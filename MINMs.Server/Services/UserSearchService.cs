using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Options;
using MySqlConnector;
using StackExchange.Redis;

namespace MINMs.Server.Services;

public interface IUserSearchService
{
    Task<UserPublicDto?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPublicDto>> SearchByUsernameAsync(
    string query,
    int limit,
    CancellationToken cancellationToken = default);
}

/// <summary>
/// Чтение публичных полей пользователей из таблицы <c>users</c> и поиск по шаблону.
/// </summary>
public sealed class UserSearchService(
    IDbConnectionFactory connectionFactory,
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisOptions> redisOptions) : IUserSearchService
{
    private const int MaxLimit = 50;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(Math.Clamp(redisOptions.Value.UserSearchCacheTtlSeconds, 5, 3600));
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    /// <summary>Возвращает карточку пользователя по первичному ключу или <c>null</c>.</summary>
    public async Task<UserPublicDto?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"minms:users:by-id:{userId}";
        var cached = await TryGetCachedAsync<UserPublicDto>(cacheKey).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        var dto = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT user_id, login, username, user_created_at
                FROM users
                WHERE user_id = @id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@id", userId);

            await using var reader = await cmd.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var createdAt = reader.GetDateTime(reader.GetOrdinal("user_created_at"));
            return new UserPublicDto
            {
                UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                Login = reader.GetString(reader.GetOrdinal("login")),
                Username = reader.GetString(reader.GetOrdinal("username")),
                UserCreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
            };
        }, cancellationToken).ConfigureAwait(false);

        if (dto is not null)
            await TrySetCachedAsync(cacheKey, dto).ConfigureAwait(false);

        return dto;
    }

    /// <summary>
    /// Поиск по подстроке в <c>login</c> и <c>username</c>; спецсимволы LIKE экранируются.
    /// </summary>
    public async Task<IReadOnlyList<UserPublicDto>> SearchByUsernameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        limit = Math.Clamp(limit, 1, MaxLimit);
        var normalizedTerm = term.ToLowerInvariant();
        var cacheKey = $"minms:users:search:{limit}:{normalizedTerm}";
        var cached = await TryGetCachedAsync<List<UserPublicDto>>(cacheKey).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        var pattern = "%" + EscapeLikePattern(term) + "%";

        var results = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT user_id, login, username, user_created_at
                FROM users
                WHERE login LIKE @pattern ESCAPE '\\'
                   OR username LIKE @pattern ESCAPE '\\'
                ORDER BY login
                LIMIT @limit
                """;
            cmd.Parameters.AddWithValue("@pattern", pattern);
            cmd.Parameters.AddWithValue("@limit", limit);

            var results = new List<UserPublicDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new UserPublicDto
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("user_id")),
                    Login = reader.GetString(reader.GetOrdinal("login")),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    UserCreatedAt = reader.GetDateTime(reader.GetOrdinal("user_created_at")),
                });
            }

            return results;
        }, cancellationToken).ConfigureAwait(false);

        await TrySetCachedAsync(cacheKey, results).ConfigureAwait(false);
        return results;
    }

    private static string EscapeLikePattern(string input) =>
        input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    private async Task<T?> TryGetCachedAsync<T>(string key)
    {
        try
        {
            var raw = await _redis.StringGetAsync(key).ConfigureAwait(false);
            if (raw.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>(raw.ToString(), JsonOptions);
        }
        catch (RedisException)
        {
            return default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private async Task TrySetCachedAsync<T>(string key, T value)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _redis.StringSetAsync(key, payload, _cacheTtl).ConfigureAwait(false);
        }
        catch (RedisException)
        {
            // Redis-кэш не должен ломать основной сценарий поиска.
        }
    }
}
