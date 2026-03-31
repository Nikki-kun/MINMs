using BCrypt.Net;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MySqlConnector;

namespace MINMs.Server.Services;

public sealed class AuthService(IDbConnectionFactory connectionFactory, JwtTokenService jwtTokenService)
{
    public async Task<RegisterOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 11);

        try
        {
            var (userId, createdAt) = await connectionFactory.WithConnectionAsync<(int UserId, DateTime UserCreatedAt)>(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    INSERT INTO users (username, password_hash, online, user_last_seen, user_created_at)
                    VALUES (@username, @password_hash, 0, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """;
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@password_hash", passwordHash);
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                var newId = (int)cmd.LastInsertedId;

                await using var readCmd = mysql.CreateCommand();
                readCmd.CommandText =
                    """
                    SELECT user_created_at
                    FROM users
                    WHERE user_id = @id
                    LIMIT 1
                    """;
                readCmd.Parameters.AddWithValue("@id", newId);
                var createdAtObj = await readCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                var createdAt = createdAtObj is DateTime dt
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : DateTime.UtcNow;

                return (newId, createdAt);
            }, cancellationToken).ConfigureAwait(false);

            var token = jwtTokenService.CreateAccessToken(userId, username);
            return RegisterOutcome.Created(ToResponse(token, userId, username, createdAt));
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry || ex.Number == 1062)
        {
            return RegisterOutcome.DuplicateUsername;
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();

        var row = await connectionFactory.WithConnectionAsync<(int UserId, string? PasswordHash, DateTime? UserCreatedAt)>(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT user_id, password_hash, user_created_at
                FROM users
                WHERE username = @username
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@username", username);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return (UserId: 0, PasswordHash: (string?)null, UserCreatedAt: (DateTime?)null);

            var id = reader.GetInt32(reader.GetOrdinal("user_id"));
            var hash = reader.GetString(reader.GetOrdinal("password_hash"));
            var createdAt = reader.GetDateTime(reader.GetOrdinal("user_created_at"));
            return (UserId: id, PasswordHash: hash, UserCreatedAt: createdAt);
        }, cancellationToken).ConfigureAwait(false);

        if (row.PasswordHash is null || !TryVerifyBcryptPassword(request.Password, row.PasswordHash))
            return null;

        var token = jwtTokenService.CreateAccessToken(row.UserId, username);
        var createdAt = row.UserCreatedAt ?? DateTime.UtcNow;
        return ToResponse(token, row.UserId, username, createdAt);
    }

    private static bool TryVerifyBcryptPassword(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (SaltParseException)
        {
            return false;
        }
    }

    private static AuthResponse ToResponse(AuthTokenResult token, int userId, string username, DateTime userCreatedAt) =>
        new()
        {
            AccessToken = token.Token,
            ExpiresInSeconds = (int)Math.Max(1, (token.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
            UserId = userId,
            Username = username,
            UserCreatedAt = userCreatedAt,
        };
}

public enum RegisterOutcomeKind
{
    Created,
    DuplicateUsername,
}

public sealed record RegisterOutcome(RegisterOutcomeKind Kind, AuthResponse? Response)
{
    public static RegisterOutcome Created(AuthResponse response) => new(RegisterOutcomeKind.Created, response);
    public static RegisterOutcome DuplicateUsername => new(RegisterOutcomeKind.DuplicateUsername, null);
}
