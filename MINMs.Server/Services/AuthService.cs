using BCrypt.Net;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MySqlConnector;

namespace MINMs.Server.Services;

public interface IAuthService
{
    Task<RegisterOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}


/// <summary>
/// Регистрация и вход: запись в MySQL, хеш пароля BCrypt, выдача JWT через <see cref="JwtTokenService"/>.
/// </summary>
public sealed class AuthService(IDbConnectionFactory connectionFactory, JwtTokenService jwtTokenService, IJwtSessionService jwtSessionService) : IAuthService
{
    public async Task<RegisterOutcome> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var login = UserLoginNormalizer.Normalize(request.Login);
        if (!UserLoginNormalizer.IsValid(login))
            return RegisterOutcome.InvalidLogin;

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
                    INSERT INTO users (username, login, password_hash, online, user_last_seen, user_created_at)
                    VALUES (@username, @login, @password_hash, 0, UTC_TIMESTAMP(), UTC_TIMESTAMP())
                    """;
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@login", login);
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

            var jti = Guid.NewGuid().ToString("N");
            var token = jwtTokenService.CreateAccessToken(userId, login, jti);
            await jwtSessionService.CreateAsync(jti, userId, login, token.ExpiresAtUtc, cancellationToken).ConfigureAwait(false);
            return RegisterOutcome.Created(ToResponse(token, userId, login, username, createdAt));
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry || ex.Number == 1062)
        {
            return RegisterOutcome.DuplicateLogin;
        }
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var login = UserLoginNormalizer.Normalize(request.Login);
        if (!UserLoginNormalizer.IsValid(login))
            return null;

        var row = await connectionFactory.WithConnectionAsync<(int UserId, string PasswordHash, string Username, DateTime UserCreatedAt)?>(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT user_id, password_hash, username, user_created_at
                FROM users
                WHERE login = @login
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@login", login);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var id = reader.GetInt32(reader.GetOrdinal("user_id"));
            var hash = reader.GetString(reader.GetOrdinal("password_hash"));
            var displayUsername = reader.GetString(reader.GetOrdinal("username"));
            var createdAt = reader.GetDateTime(reader.GetOrdinal("user_created_at"));
            return (id, hash, displayUsername, createdAt);
        }, cancellationToken).ConfigureAwait(false);

        if (row is null)
            return null;

        if (!TryVerifyBcryptPassword(request.Password, row.Value.PasswordHash))
            return null;

        var jti = Guid.NewGuid().ToString("N");
        var token = jwtTokenService.CreateAccessToken(row.Value.UserId, login, jti);
        await jwtSessionService.CreateAsync(jti, row.Value.UserId, login, token.ExpiresAtUtc, cancellationToken).ConfigureAwait(false);
        return ToResponse(token, row.Value.UserId, login, row.Value.Username, row.Value.UserCreatedAt);
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

    private static AuthResponse ToResponse(AuthTokenResult token, int userId, string login, string username, DateTime userCreatedAt) =>
        new()
        {
            AccessToken = token.Token,
            ExpiresInSeconds = (int)Math.Max(1, (token.ExpiresAtUtc - DateTime.UtcNow).TotalSeconds),
            UserId = userId,
            Login = login,
            Username = username,
            UserCreatedAt = userCreatedAt,
        };
}

public enum RegisterOutcomeKind
{
    Created,
    DuplicateLogin,
    InvalidLogin,
}

public sealed record RegisterOutcome(RegisterOutcomeKind Kind, AuthResponse? Response)
{
    public static RegisterOutcome Created(AuthResponse response) => new(RegisterOutcomeKind.Created, response);
    public static RegisterOutcome DuplicateLogin => new(RegisterOutcomeKind.DuplicateLogin, null);
    public static RegisterOutcome InvalidLogin => new(RegisterOutcomeKind.InvalidLogin, null);
}
