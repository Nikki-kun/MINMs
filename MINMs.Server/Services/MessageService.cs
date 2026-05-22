using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MySqlConnector;
using StackExchange.Redis;
using System.Data;
using System.Text.Json;

namespace MINMs.Server.Services;

public interface IMessageService
{
    Task<SendMessageResult> SendMessageAsync(
        int senderId,
        int chatId,
        string content,
        MessageType type = MessageType.Text,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        int userId,
        int chatId,
        int offset = 0,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<MessageDto?> GetMessageByIdAsync(
        int userId,
        int messageId,
        CancellationToken cancellationToken = default);

    Task<DeleteMessageResult> DeleteMessageAsync(
        int userId,
        int messageId,
        CancellationToken cancellationToken = default);

    Task<bool> CanUserAccessChatAsync(
        int userId,
        int chatId,
        CancellationToken cancellationToken = default);

    Task<bool> IsUserBlockedInChatAsync(
        int userId,
        int chatId,
        CancellationToken cancellationToken = default);
}

public sealed class MessageService(
    IDbConnectionFactory connectionFactory,
    IConnectionMultiplexer connectionMultiplexer) : IMessageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    private const int MaxMessageLength = 1000;
    private const int DefaultMessagesLimit = 50;
    private const int MaxMessagesLimit = 200;

    /// <summary>
    /// Отправка сообщения в чат с проверками: участник ли пользователь, не заблокирован ли.
    /// </summary>
    public async Task<SendMessageResult> SendMessageAsync(
        int senderId,
        int chatId,
        string content,
        MessageType type = MessageType.Text,
        CancellationToken cancellationToken = default)
    {
        if (senderId <= 0 || chatId <= 0)
            return SendMessageResult.InvalidInput;

        var normalizedContent = (content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
            return SendMessageResult.EmptyContent;

        if (normalizedContent.Length > MaxMessageLength)
            return SendMessageResult.ContentTooLong;

        var canAccess = await CanUserAccessChatAsync(senderId, chatId, cancellationToken);
        if (!canAccess)
            return SendMessageResult.AccessDenied;

        var isBlocked = await IsUserBlockedInChatAsync(senderId, chatId, cancellationToken);
        if (isBlocked)
            return SendMessageResult.UserBlocked;

        try
        {
            var messageId = await connectionFactory.WithConnectionAsync(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    INSERT INTO messages (sender_id, chat_id, content, type, message_created_at, status)
                    VALUES (@senderId, @chatId, @content, @type, UTC_TIMESTAMP(), 0);
                    SELECT LAST_INSERT_ID();
                    """;
                cmd.Parameters.AddWithValue("@senderId", senderId);
                cmd.Parameters.AddWithValue("@chatId", chatId);
                cmd.Parameters.AddWithValue("@content", normalizedContent);
                cmd.Parameters.AddWithValue("@type", (sbyte)type);

                var id = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt32(id);
            }, cancellationToken).ConfigureAwait(false);

            // Инвалидируем кэш сообщений чата
            await TryInvalidateChatMessagesCacheAsync(chatId).ConfigureAwait(false);

            return SendMessageResult.Success;
        }
        catch (MySqlException ex)
        {
            if (ex.ErrorCode == MySqlErrorCode.NoReferencedRow || ex.Number == 1452)
                return SendMessageResult.AccessDenied;
            return SendMessageResult.DatabaseError;
        }
    }

    public async Task<IReadOnlyList<MessageDto>> GetMessagesAsync(
        int userId,
        int chatId,
        int offset = 0,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || chatId <= 0)
            return [];

        limit = Math.Clamp(limit, 1, MaxMessagesLimit);
        offset = Math.Max(0, offset);

        // Проверка доступа к чату
        var canAccess = await CanUserAccessChatAsync(userId, chatId, cancellationToken);
        if (!canAccess)
            return [];

        var cacheKey = $"minms:messages:chat:{chatId}:offset:{offset}:limit:{limit}";
        var cached = await TryGetCachedAsync<List<MessageDto>>(cacheKey).ConfigureAwait(false);
        if (cached is not null)
            return cached;

        var messages = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    m.message_id,
                    m.sender_id,
                    u.login as sender_login,
                    u.username as sender_username,
                    m.chat_id,
                    m.content,
                    m.message_created_at,
                    m.status,
                    m.type
                FROM messages m
                JOIN users u ON u.user_id = m.sender_id
                WHERE m.chat_id = @chatId
                ORDER BY m.message_created_at DESC, m.message_id DESC
                LIMIT @limit OFFSET @offset
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@limit", limit);
            cmd.Parameters.AddWithValue("@offset", offset);

            var result = new List<MessageDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var createdAt = reader.GetDateTime(reader.GetOrdinal("message_created_at"));
                result.Add(new MessageDto
                {
                    MessageId = reader.GetInt32(reader.GetOrdinal("message_id")),
                    SenderId = reader.GetInt32(reader.GetOrdinal("sender_id")),
                    SenderLogin = reader.GetString(reader.GetOrdinal("sender_login")),
                    SenderUsername = reader.GetString(reader.GetOrdinal("sender_username")),
                    ChatId = reader.GetInt32(reader.GetOrdinal("chat_id")),
                    Content = reader.GetString(reader.GetOrdinal("content")),
                    MessageCreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                    Status = (MessageStatus)reader.GetSByte(reader.GetOrdinal("status")),
                    Type = (MessageType)reader.GetSByte(reader.GetOrdinal("type"))
                });
            }

            return result;
        }, cancellationToken).ConfigureAwait(false);

        await TrySetCachedAsync(cacheKey, messages).ConfigureAwait(false);
        return messages;
    }

    /// <summary>
    /// Получение одного сообщения по ID с проверкой доступа.
    /// </summary>
    public async Task<MessageDto?> GetMessageByIdAsync(
        int userId,
        int messageId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || messageId <= 0)
            return null;

        var message = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    m.message_id,
                    m.sender_id,
                    u.login as sender_login,
                    u.username as sender_username,
                    m.chat_id,
                    m.content,
                    m.message_created_at,
                    m.status,
                    m.type
                FROM messages m
                JOIN users u ON u.user_id = m.sender_id
                WHERE m.message_id = @messageId
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@messageId", messageId);

            await using var reader = await cmd.ExecuteReaderAsync(
                CommandBehavior.SingleRow,
                cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return null;

            var chatId = reader.GetInt32(reader.GetOrdinal("chat_id"));

            // Проверка доступа к чату
            var canAccess = await CanUserAccessChatAsync(userId, chatId, cancellationToken);
            if (!canAccess)
                return null;

            var createdAt = reader.GetDateTime(reader.GetOrdinal("message_created_at"));
            return new MessageDto
            {
                MessageId = reader.GetInt32(reader.GetOrdinal("message_id")),
                SenderId = reader.GetInt32(reader.GetOrdinal("sender_id")),
                SenderLogin = reader.GetString(reader.GetOrdinal("sender_login")),
                SenderUsername = reader.GetString(reader.GetOrdinal("sender_username")),
                ChatId = chatId,
                Content = reader.GetString(reader.GetOrdinal("content")),
                MessageCreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc),
                Status = (MessageStatus)reader.GetSByte(reader.GetOrdinal("status")),
                Type = (MessageType)reader.GetSByte(reader.GetOrdinal("type"))
            };
        }, cancellationToken).ConfigureAwait(false);

        return message;
    }

    public async Task<DeleteMessageResult> DeleteMessageAsync(
        int userId,
        int messageId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || messageId <= 0)
            return DeleteMessageResult.InvalidInput;

        try
        {
            var (affectedRows, chatId) = await connectionFactory.WithConnectionAsync(async connection =>
            {
                if (connection is not MySqlConnection mysql)
                    throw new InvalidOperationException("Expected MySqlConnection.");

                await using var cmd = mysql.CreateCommand();
                cmd.CommandText =
                    """
                    DELETE FROM messages
                    WHERE message_id = @messageId AND sender_id = @userId;
                    SELECT ROW_COUNT(), chat_id FROM messages WHERE message_id = @messageId;
                    """;
                cmd.Parameters.AddWithValue("@messageId", messageId);
                cmd.Parameters.AddWithValue("@userId", userId);

                var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                cmd.CommandText = "SELECT chat_id FROM messages WHERE message_id = @messageId";
                var chatIdResult = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

                return (deleted, chatIdResult != null ? Convert.ToInt32(chatIdResult) : 0);
            }, cancellationToken).ConfigureAwait(false);

            if (affectedRows == 0)
                return DeleteMessageResult.NotFound;

            if (chatId > 0)
                await TryInvalidateChatMessagesCacheAsync(chatId).ConfigureAwait(false);

            return DeleteMessageResult.Success;
        }
        catch (MySqlException)
        {
            return DeleteMessageResult.DatabaseError;
        }
    }

    public async Task<bool> CanUserAccessChatAsync(
        int userId,
        int chatId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || chatId <= 0)
            return false;

        var cacheKey = $"minms:chat:access:{userId}:{chatId}";
        var cached = await TryGetCachedAsync<bool?>(cacheKey).ConfigureAwait(false);
        if (cached.HasValue)
            return cached.Value;

        var canAccess = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT COUNT(1)
                FROM chat_participants
                WHERE chat_id = @chatId
                  AND user_id = @userId
                  AND membership_status = 0  -- 0 = active (не left, не banned)
                  AND (banned_at IS NULL OR banned_at > UTC_TIMESTAMP())
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@userId", userId);

            var count = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(count) > 0;
        }, cancellationToken).ConfigureAwait(false);

        await TrySetCachedAsync(cacheKey, canAccess, TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        return canAccess;
    }

    public async Task<bool> IsUserBlockedInChatAsync(
        int userId,
        int chatId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || chatId <= 0)
            return true;

        var result = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText =
                """
                SELECT COUNT(1)
                FROM chat_participants
                WHERE chat_id = @chatId
                  AND user_id = @userId
                  AND membership_status = 2  -- 2 = banned
                  AND (banned_at IS NOT NULL AND banned_at <= UTC_TIMESTAMP())
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@userId", userId);

            var count = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt32(count) > 0;
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static string GetChatMessagesCacheKey(int chatId) => $"minms:messages:chat:{chatId}:*";

    private async Task TryInvalidateChatMessagesCacheAsync(int chatId)
    {
        try
        {
            var server = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints().First());
            var keys = server.Keys(pattern: $"minms:messages:chat:{chatId}:*").ToArray();
            if (keys.Length > 0)
                await _redis.KeyDeleteAsync(keys).ConfigureAwait(false);
        }
        catch (RedisException)
        {
            try
            {
                var endpoints = connectionMultiplexer.GetEndPoints();
                foreach (var endpoint in endpoints)
                {
                    var server = connectionMultiplexer.GetServer(endpoint);
                    var keys = server.Keys(pattern: $"minms:messages:chat:{chatId}:*").ToArray();
                    if (keys.Length > 0)
                        await _redis.KeyDeleteAsync(keys).ConfigureAwait(false);
                }
            }
            catch
            {

            }
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

    private async Task TrySetCachedAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _redis.StringSetAsync(key, payload, ttl ?? _cacheTtl).ConfigureAwait(false);
        }
        catch (RedisException)
        {

        }
    }
}

public enum SendMessageResult
{
    Success,
    InvalidInput,
    EmptyContent,
    ContentTooLong,
    AccessDenied,
    UserBlocked,
    DatabaseError
}

public enum DeleteMessageResult
{
    Success,
    NotFound,
    AccessDenied,
    DatabaseError,
    InvalidInput
}
