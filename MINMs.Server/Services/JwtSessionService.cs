using System.Globalization;
using StackExchange.Redis;

namespace MINMs.Server.Services;

public interface IJwtSessionService
{
    Task CreateAsync(string jti, int userId, string login, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(string jti, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string jti, CancellationToken cancellationToken = default);
}

/// <summary>
/// Привязка “сессии” к JWT через jti:
/// при каждом запросе валидатор JWT проверяет, что ключ jti существует в Redis.
/// </summary>
public sealed class RedisJwtSessionService(IConnectionMultiplexer connectionMultiplexer) : IJwtSessionService
{
    private const string KeyPrefix = "minms:jwt:sessions:";

    private static readonly TimeSpan TtlExtra = TimeSpan.FromMinutes(2);

    public async Task CreateAsync(string jti, int userId, string login, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(jti))
            throw new ArgumentException("jti is required.", nameof(jti));

        var ttl = expiresAtUtc - DateTime.UtcNow + TtlExtra;
        if (ttl <= TimeSpan.Zero)
            ttl = TimeSpan.FromSeconds(1);

        var key = KeyPrefix + jti;
        var value = $"{userId.ToString(CultureInfo.InvariantCulture)}:{login}";

        var db = connectionMultiplexer.GetDatabase();
        await db.StringSetAsync(key, value, ttl).ConfigureAwait(false);
    }

    public async Task<bool> IsActiveAsync(string jti, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(jti))
            return false;

        var key = KeyPrefix + jti;
        var db = connectionMultiplexer.GetDatabase();
        return await db.KeyExistsAsync(key).ConfigureAwait(false);
    }

    public async Task InvalidateAsync(string jti, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(jti))
            return;

        var key = KeyPrefix + jti;
        var db = connectionMultiplexer.GetDatabase();
        await db.KeyDeleteAsync(key).ConfigureAwait(false);
    }
}

