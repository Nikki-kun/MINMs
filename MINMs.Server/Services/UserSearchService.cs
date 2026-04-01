using System.Data;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MySqlConnector;

namespace MINMs.Server.Services;

public sealed class UserSearchService(IDbConnectionFactory connectionFactory)
{
    private const int MaxLimit = 50;

    public async Task<UserPublicDto?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        await connectionFactory.WithConnectionAsync(async connection =>
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

    public async Task<IReadOnlyList<UserPublicDto>> SearchByUsernameAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var term = query.Trim();
        if (term.Length == 0)
            return [];

        limit = Math.Clamp(limit, 1, MaxLimit);
        var pattern = "%" + EscapeLikePattern(term) + "%";

        return await connectionFactory.WithConnectionAsync(async connection =>
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
    }

    private static string EscapeLikePattern(string input) =>
        input
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
