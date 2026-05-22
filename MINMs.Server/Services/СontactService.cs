using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MySqlConnector;
using StackExchange.Redis;
using System.Data;
using System.Text.Json;

namespace MINMs.Server.Services;

public interface IContactService
{
    Task<AddContactResult> AddContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContactPublicDto>> GetContactsAsync(int ownerId, CancellationToken cancellationToken = default);
    Task<UpdateContactResult> UpdateContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default);
    Task<DeleteContactResult> DeleteContactAsync(int ownerId, string contactLogin, CancellationToken cancellationToken = default);
}

public sealed class ContactService(
    IUserSearchService userSearchService,
    IDbConnectionFactory connectionFactory,
    IConnectionMultiplexer connectionMultiplexer) : IContactService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

    public async Task<AddContactResult> AddContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default)
    {
        if (ownerId <= 0)
            return AddContactResult.InvalidInput;

        var normalizedLogin = UserLoginNormalizer.Normalize(contactLogin);
        var normalizedContactName = (contactName ?? string.Empty).Trim();

        if (!UserLoginNormalizer.IsValid(normalizedLogin))
            return AddContactResult.InvalidInput;

        if (normalizedContactName.Length is 0 or > 100)
            return AddContactResult.InvalidInput;

        var contact = await userSearchService.GetInternalByUserLoginAsync(normalizedLogin, cancellationToken).ConfigureAwait(false);
        if (contact is null)
            return AddContactResult.ContactNotFound;

        if (contact.UserId == ownerId)
            return AddContactResult.CannotAddSelf;

        try
        {
            var insertedRows = await connectionFactory.WithConnectionAsync(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    INSERT INTO contacts (owner_id, contact_id, contact_name, contact_added_at)
                    VALUES (@ownerId, @contactId, @contactName, UTC_TIMESTAMP())
                    """;
                cmd.Parameters.AddWithValue("@ownerId", ownerId);
                cmd.Parameters.AddWithValue("@contactId", contact.UserId);
                cmd.Parameters.AddWithValue("@contactName", normalizedContactName);

                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            if (insertedRows > 0)
            {
                await TryInvalidateOwnerContactsCacheAsync(ownerId).ConfigureAwait(false);
                return AddContactResult.Success;
            }
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry || ex.Number == 1062)
        {
            return AddContactResult.AlreadyExists;
        }
        catch (MySqlException)
        {
            return AddContactResult.DatabaseError;
        }

        return AddContactResult.DatabaseError;
    }

    public async Task<IReadOnlyList<ContactPublicDto>> GetContactsAsync(int ownerId, CancellationToken cancellationToken = default)
    {
        if (ownerId <= 0)
            return [];

        var cacheKey = GetOwnerContactsCacheKey(ownerId);
        var cached = await TryGetCachedAsync<List<ContactPublicDto>>(cacheKey).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        var contacts = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT u.login, u.username, c.contact_name, c.contact_added_at
                FROM contacts c
                JOIN users u ON u.user_id = c.contact_id
                WHERE c.owner_id = @ownerId
                ORDER BY c.contact_added_at DESC, u.login ASC
                """;
            cmd.Parameters.AddWithValue("@ownerId", ownerId);

            var result = new List<ContactPublicDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var addedAt = reader.GetDateTime(reader.GetOrdinal("contact_added_at"));
                result.Add(new ContactPublicDto
                {
                    Login = reader.GetString(reader.GetOrdinal("login")),
                    Username = reader.GetString(reader.GetOrdinal("username")),
                    ContactName = reader.GetString(reader.GetOrdinal("contact_name")),
                    ContactAddedAt = DateTime.SpecifyKind(addedAt, DateTimeKind.Utc),
                });
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);

        await TrySetCachedAsync(cacheKey, contacts).ConfigureAwait(false);
        return contacts;
    }

    public async Task<UpdateContactResult> UpdateContactAsync(int ownerId, string contactLogin, string contactName, CancellationToken cancellationToken = default)
    {
        if (ownerId <= 0)
            return UpdateContactResult.InvalidInput;

        var normalizedLogin = UserLoginNormalizer.Normalize(contactLogin);
        var normalizedContactName = (contactName ?? string.Empty).Trim();
        if (!UserLoginNormalizer.IsValid(normalizedLogin) || normalizedContactName.Length is 0 or > 100)
            return UpdateContactResult.InvalidInput;

        var contact = await userSearchService.GetInternalByUserLoginAsync(normalizedLogin, cancellationToken).ConfigureAwait(false);
        if (contact is null)
            return UpdateContactResult.ContactNotFound;

        try
        {
            var updatedRows = await connectionFactory.WithConnectionAsync(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    UPDATE contacts
                    SET contact_name = @contactName
                    WHERE owner_id = @ownerId AND contact_id = @contactId
                    """;
                cmd.Parameters.AddWithValue("@ownerId", ownerId);
                cmd.Parameters.AddWithValue("@contactId", contact.UserId);
                cmd.Parameters.AddWithValue("@contactName", normalizedContactName);

                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            if (updatedRows == 0)
                return UpdateContactResult.ContactNotFound;

            await TryInvalidateOwnerContactsCacheAsync(ownerId).ConfigureAwait(false);
            return UpdateContactResult.Success;
        }
        catch (MySqlException)
        {
            return UpdateContactResult.DatabaseError;
        }
    }

    public async Task<DeleteContactResult> DeleteContactAsync(int ownerId, string contactLogin, CancellationToken cancellationToken = default)
    {
        if (ownerId <= 0)
            return DeleteContactResult.InvalidInput;

        var normalizedLogin = UserLoginNormalizer.Normalize(contactLogin);
        if (!UserLoginNormalizer.IsValid(normalizedLogin))
            return DeleteContactResult.InvalidInput;

        var contact = await userSearchService.GetInternalByUserLoginAsync(normalizedLogin, cancellationToken).ConfigureAwait(false);
        if (contact is null)
            return DeleteContactResult.ContactNotFound;

        try
        {
            var deletedRows = await connectionFactory.WithConnectionAsync(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    DELETE FROM contacts
                    WHERE owner_id = @ownerId AND contact_id = @contactId
                    """;
                cmd.Parameters.AddWithValue("@ownerId", ownerId);
                cmd.Parameters.AddWithValue("@contactId", contact.UserId);

                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);

            if (deletedRows == 0)
                return DeleteContactResult.ContactNotFound;

            await TryInvalidateOwnerContactsCacheAsync(ownerId).ConfigureAwait(false);
            return DeleteContactResult.Success;
        }
        catch (MySqlException)
        {
            return DeleteContactResult.DatabaseError;
        }
    }

    private static string GetOwnerContactsCacheKey(int ownerId) => $"minms:contacts:owner:{ownerId}";

    private async Task TryInvalidateOwnerContactsCacheAsync(int ownerId)
    {
        try
        {
            await _redis.KeyDeleteAsync(GetOwnerContactsCacheKey(ownerId)).ConfigureAwait(false);
        }
        catch (RedisException)
        {
        }
    }

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
        }
    }
}

public enum AddContactResult
{
    Success,
    AlreadyExists,
    ContactNotFound,
    CannotAddSelf,
    DatabaseError,
    InvalidInput
}

public enum UpdateContactResult
{
    Success,
    ContactNotFound,
    DatabaseError,
    InvalidInput
}

public enum DeleteContactResult
{
    Success,
    ContactNotFound,
    DatabaseError,
    InvalidInput
}
