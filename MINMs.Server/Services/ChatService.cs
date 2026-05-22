using Microsoft.Extensions.Options;
using MINMs.Server.Database;
using MINMs.Server.Models.Dtos;
using MINMs.Server.Options;
using MySqlConnector;
using StackExchange.Redis;
using System.Data;
using System.Text.Json;

namespace MINMs.Server.Services;

public interface IChatService
{
    Task<ChatDto?> CreateChatAsync(CreateChatRequest request, int creatorUserId, CancellationToken cancellationToken = default);
    Task<bool> DeleteChatAsync(int chatId, int requesterUserId, CancellationToken cancellationToken = default);
    Task<ChatDto?> GetChatByIdAsync(int chatId, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatPreviewDto>> GetUserChatsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> AddParticipantAsync(int chatId, int ownerUserId, int targetUserId, CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(int chatId, int ownerUserId, int targetUserId, CancellationToken cancellationToken = default);
    Task<bool> LeaveChatAsync(int chatId, int userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Управление чатами: создание, удаление, участники.
/// </summary>
public sealed class ChatService(
    IDbConnectionFactory connectionFactory,
    IConnectionMultiplexer connectionMultiplexer,
    IOptions<RedisOptions> redisOptions) : IChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(Math.Clamp(redisOptions.Value.UserSearchCacheTtlSeconds, 5, 3600));
    private readonly IDatabase _redis = connectionMultiplexer.GetDatabase();

    /// <summary>
    /// Создаёт новый чат. Для личного чата (type=1) проверяет, что чат между этими пользователями ещё не существует.
    /// </summary>
    public async Task<ChatDto?> CreateChatAsync(CreateChatRequest request, int creatorUserId, CancellationToken cancellationToken = default)
    {
        if (request.Type == ChatType.Personal && request.ParticipantIds?.Count != 1)
            throw new ArgumentException("Личный чат должен содержать ровно одного участника (кроме создателя)");

        if (request.Type == ChatType.Group && (request.ParticipantIds?.Count < 1))
            throw new ArgumentException("Групповой чат должен содержать хотя бы одного участника");

        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            // Для личных чатов проверяем существующий
            if (request.Type == ChatType.Personal)
            {
                var otherUserId = request.ParticipantIds![0];
                var existingChatId = await GetExistingPersonalChatAsync(mysql, creatorUserId, otherUserId, cancellationToken);
                if (existingChatId.HasValue)
                    return await GetChatByIdAsync(existingChatId.Value, creatorUserId, cancellationToken);
            }

            await using var transaction = await mysql.BeginTransactionAsync(cancellationToken);

            try
            {
                // Создаём чат
                await using var insertChatCmd = mysql.CreateCommand();
                insertChatCmd.Transaction = transaction;
                insertChatCmd.CommandText = """
                    INSERT INTO chats (type, chat_created_at)
                    VALUES (@type, UTC_TIMESTAMP());
                    SELECT LAST_INSERT_ID();
                    """;
                insertChatCmd.Parameters.AddWithValue("@type", (sbyte)request.Type);

                var chatId = Convert.ToInt32(await insertChatCmd.ExecuteScalarAsync(cancellationToken));

                // Добавляем создателя
                await AddParticipantInternalAsync(mysql, transaction, chatId, creatorUserId, ParticipantRole.Owner, cancellationToken);

                // Добавляем остальных участников
                if (request.ParticipantIds != null)
                {
                    foreach (var participantId in request.ParticipantIds.Distinct())
                    {
                        if (participantId == creatorUserId)
                            continue;

                        await AddParticipantInternalAsync(mysql, transaction, chatId, participantId, ParticipantRole.Member, cancellationToken);
                    }
                }

                await transaction.CommitAsync(cancellationToken);

                // Инвалидируем кэши после создания
                await InvalidateUserChatsCacheAsync(creatorUserId);
                if (request.ParticipantIds != null)
                {
                    foreach (var pid in request.ParticipantIds)
                        await InvalidateUserChatsCacheAsync(pid);
                }

                return await GetChatByIdAsync(chatId, creatorUserId, cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }, cancellationToken);
    }

    private async Task<int?> GetExistingPersonalChatAsync(MySqlConnection connection, int userId1, int userId2, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT cp1.chat_id
            FROM chat_participants cp1
            INNER JOIN chat_participants cp2 ON cp1.chat_id = cp2.chat_id
            INNER JOIN chats c ON cp1.chat_id = c.chat_id
            WHERE c.type = 1
              AND cp1.user_id = @uid1
              AND cp2.user_id = @uid2
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@uid1", userId1);
        cmd.Parameters.AddWithValue("@uid2", userId2);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null ? Convert.ToInt32(result) : null;
    }

    private async Task AddParticipantInternalAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int chatId,
        int userId,
        ParticipantRole role,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = """
            INSERT INTO chat_participants (chat_id, user_id, participant_role, joined_at)
            VALUES (@chatId, @userId, @role, UTC_TIMESTAMP())
            ON DUPLICATE KEY UPDATE
                left_at = NULL,
                banned_at = NULL,
                participant_role = VALUES(participant_role),
                membership_status = 0
            """;
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@role", (sbyte)role);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Удаляет чат. Только владелец может удалить личный чат, для группы достаточно быть участником?
    /// </summary>
    public async Task<bool> DeleteChatAsync(int chatId, int requesterUserId, CancellationToken cancellationToken = default)
    {
        // Проверяем права: владелец чата может удалить (или любой участник? пусть владелец)
        var isOwner = await IsChatOwnerAsync(chatId, requesterUserId, cancellationToken);
        if (!isOwner)
            return false;

        var cacheKey = $"minms:chat:{chatId}";

        var result = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = "DELETE FROM chats WHERE chat_id = @chatId";
            cmd.Parameters.AddWithValue("@chatId", chatId);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return rows > 0;
        }, cancellationToken);

        if (result)
        {
            await _redis.KeyDeleteAsync(cacheKey);
            await InvalidateChatParticipantsCacheAsync(chatId);
        }

        return result;
    }

    /// <summary>
    /// Получает чат с участниками.
    /// </summary>
    public async Task<ChatDto?> GetChatByIdAsync(int chatId, int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"minms:chat:{chatId}:user:{userId}";
        var cached = await TryGetCachedAsync<ChatDto>(cacheKey);
        if (cached is not null)
            return cached;

        var dto = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            // Проверяем, что пользователь участник чата
            await using var checkCmd = mysql.CreateCommand();
            checkCmd.CommandText = "SELECT 1 FROM chat_participants WHERE chat_id = @chatId AND user_id = @userId AND left_at IS NULL";
            checkCmd.Parameters.AddWithValue("@chatId", chatId);
            checkCmd.Parameters.AddWithValue("@userId", userId);

            var isParticipant = await checkCmd.ExecuteScalarAsync(cancellationToken) is not null;
            if (!isParticipant)
                return null;

            // Получаем информацию о чате
            await using var chatCmd = mysql.CreateCommand();
            chatCmd.CommandText = """
                SELECT chat_id, type, chat_created_at
                FROM chats
                WHERE chat_id = @chatId
                """;
            chatCmd.Parameters.AddWithValue("@chatId", chatId);

            await using var reader = await chatCmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var chat = new ChatDto
            {
                ChatId = reader.GetInt32(reader.GetOrdinal("chat_id")),
                Type = (ChatType)reader.GetSByte(reader.GetOrdinal("type")),
                ChatCreatedAt = reader.GetDateTime(reader.GetOrdinal("chat_created_at")),
                Participants = []
            };

            await reader.CloseAsync();

            // Получаем участников
            await using var participantsCmd = mysql.CreateCommand();
            participantsCmd.CommandText = """
                SELECT cp.user_id, cp.participant_role, cp.joined_at,
                       u.login, u.username, u.avatar_id
                FROM chat_participants cp
                JOIN users u ON cp.user_id = u.user_id
                WHERE cp.chat_id = @chatId AND cp.left_at IS NULL
                ORDER BY cp.participant_role ASC, cp.joined_at ASC
                """;
            participantsCmd.Parameters.AddWithValue("@chatId", chatId);

            await using var participantsReader = await participantsCmd.ExecuteReaderAsync(cancellationToken);
            while (await participantsReader.ReadAsync(cancellationToken))
            {
                chat.Participants.Add(new ChatParticipantDto
                {
                    UserId = participantsReader.GetInt32(participantsReader.GetOrdinal("user_id")),
                    Login = participantsReader.GetString(participantsReader.GetOrdinal("login")),
                    Username = participantsReader.GetString(participantsReader.GetOrdinal("username")),
                    AvatarId = participantsReader.IsDBNull(participantsReader.GetOrdinal("avatar_id")) ? null : participantsReader.GetInt32(participantsReader.GetOrdinal("avatar_id")),
                    Role = (ParticipantRole)participantsReader.GetSByte(participantsReader.GetOrdinal("participant_role")),
                    JoinedAt = participantsReader.GetDateTime(participantsReader.GetOrdinal("joined_at"))
                });
            }

            return chat;
        }, cancellationToken);

        if (dto is not null)
            await TrySetCachedAsync(cacheKey, dto, TimeSpan.FromMinutes(5)); // кэш для чатов короче

        return dto;
    }

    /// <summary>
    /// Получает список чатов пользователя (превью с последним сообщением).
    /// </summary>
    public async Task<IReadOnlyList<ChatPreviewDto>> GetUserChatsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"minms:user:{userId}:chats";
        var cached = await TryGetCachedAsync<List<ChatPreviewDto>>(cacheKey);
        if (cached is not null)
            return cached;

        var results = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = """
                SELECT
                    c.chat_id,
                    c.type,
                    c.chat_created_at,
                    (SELECT content FROM messages m
                     WHERE m.chat_id = c.chat_id
                     ORDER BY m.message_created_at DESC
                     LIMIT 1) as last_message_content,
                    (SELECT message_created_at FROM messages m
                     WHERE m.chat_id = c.chat_id
                     ORDER BY m.message_created_at DESC
                     LIMIT 1) as last_message_at,
                    (SELECT COUNT(*) FROM messages m
                     WHERE m.chat_id = c.chat_id
                     AND m.message_created_at > cp.joined_at
                     AND m.status < 2) as unread_count
                FROM chat_participants cp
                JOIN chats c ON cp.chat_id = c.chat_id
                WHERE cp.user_id = @userId AND cp.left_at IS NULL
                ORDER BY last_message_at DESC, c.chat_created_at DESC
                """;
            cmd.Parameters.AddWithValue("@userId", userId);

            var results = new List<ChatPreviewDto>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new ChatPreviewDto
                {
                    ChatId = reader.GetInt32(reader.GetOrdinal("chat_id")),
                    Type = (ChatType)reader.GetSByte(reader.GetOrdinal("type")),
                    ChatCreatedAt = reader.GetDateTime(reader.GetOrdinal("chat_created_at")),
                    LastMessageContent = reader.IsDBNull(reader.GetOrdinal("last_message_content")) ? null : reader.GetString(reader.GetOrdinal("last_message_content")),
                    LastMessageAt = reader.IsDBNull(reader.GetOrdinal("last_message_at")) ? null : reader.GetDateTime(reader.GetOrdinal("last_message_at")),
                    UnreadCount = reader.GetInt32(reader.GetOrdinal("unread_count"))
                });
            }

            return results;
        }, cancellationToken);

        await TrySetCachedAsync(cacheKey, results, TimeSpan.FromSeconds(30)); // короткий кэш для списка чатов
        return results;
    }

    public async Task<bool> AddParticipantAsync(int chatId, int ownerUserId, int targetUserId, CancellationToken cancellationToken = default)
    {
        // Только владелец может добавлять участников
        var isOwner = await IsChatOwnerAsync(chatId, ownerUserId, cancellationToken);
        if (!isOwner)
            return false;

        var chat = await GetChatBaseInfoAsync(chatId, cancellationToken);
        if (chat?.Type != ChatType.Group)
            return false;

        var result = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var transaction = await mysql.BeginTransactionAsync(cancellationToken);

            try
            {
                await AddParticipantInternalAsync(mysql, transaction, chatId, targetUserId, ParticipantRole.Member, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }, cancellationToken);

        if (result)
        {
            await InvalidateUserChatsCacheAsync(targetUserId);
            await InvalidateChatCacheAsync(chatId);
        }

        return result;
    }

    public async Task<bool> RemoveParticipantAsync(int chatId, int ownerUserId, int targetUserId, CancellationToken cancellationToken = default)
    {
        var isOwner = await IsChatOwnerAsync(chatId, ownerUserId, cancellationToken);
        if (!isOwner)
            return false;

        if (ownerUserId == targetUserId)
            return false; // владелец не может удалить сам себя, используй LeaveChatAsync

        var result = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = """
                UPDATE chat_participants
                SET left_at = UTC_TIMESTAMP(), membership_status = 1
                WHERE chat_id = @chatId AND user_id = @userId AND left_at IS NULL
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@userId", targetUserId);

            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }, cancellationToken);

        if (result)
        {
            await InvalidateUserChatsCacheAsync(targetUserId);
            await InvalidateChatCacheAsync(chatId);
        }

        return result;
    }

    public async Task<bool> LeaveChatAsync(int chatId, int userId, CancellationToken cancellationToken = default)
    {
        var chat = await GetChatBaseInfoAsync(chatId, cancellationToken);
        if (chat is null)
            return false;

        // Для личного чата — удаляем полностью
        if (chat.Type == ChatType.Personal)
        {
            return await DeleteChatAsync(chatId, userId, cancellationToken);
        }

        // Проверяем, не последний ли владелец уходит
        var ownerCount = await GetChatOwnerCountAsync(chatId, cancellationToken);
        var isOwner = await IsChatOwnerAsync(chatId, userId, cancellationToken);

        var result = await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var transaction = await mysql.BeginTransactionAsync(cancellationToken);

            try
            {
                // Помечаем как покинувшего
                await using var cmd = mysql.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    UPDATE chat_participants
                    SET left_at = UTC_TIMESTAMP(), membership_status = 1
                    WHERE chat_id = @chatId AND user_id = @userId AND left_at IS NULL
                    """;
                cmd.Parameters.AddWithValue("@chatId", chatId);
                cmd.Parameters.AddWithValue("@userId", userId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                // Если уходит владелец и есть другие участники — назначаем нового владельца
                if (isOwner && ownerCount == 1)
                {
                    await using var newOwnerCmd = mysql.CreateCommand();
                    newOwnerCmd.Transaction = transaction;
                    newOwnerCmd.CommandText = """
                        UPDATE chat_participants
                        SET participant_role = 1
                        WHERE chat_id = @chatId AND left_at IS NULL
                        ORDER BY joined_at ASC
                        LIMIT 1
                        """;
                    newOwnerCmd.Parameters.AddWithValue("@chatId", chatId);
                    await newOwnerCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }, cancellationToken);

        if (result)
        {
            await InvalidateUserChatsCacheAsync(userId);
            await InvalidateChatCacheAsync(chatId);
        }

        return result;
    }

    // Вспомогательные методы
    private async Task<bool> IsChatOwnerAsync(int chatId, int userId, CancellationToken cancellationToken)
    {
        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = """
                SELECT 1 FROM chat_participants
                WHERE chat_id = @chatId AND user_id = @userId
                AND participant_role = 1 AND left_at IS NULL
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);
            cmd.Parameters.AddWithValue("@userId", userId);

            return await cmd.ExecuteScalarAsync(cancellationToken) is not null;
        }, cancellationToken);
    }

    private async Task<int> GetChatOwnerCountAsync(int chatId, CancellationToken cancellationToken)
    {
        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM chat_participants
                WHERE chat_id = @chatId AND participant_role = 1 AND left_at IS NULL
                """;
            cmd.Parameters.AddWithValue("@chatId", chatId);

            return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        }, cancellationToken);
    }

    private async Task<ChatBaseInfo?> GetChatBaseInfoAsync(int chatId, CancellationToken cancellationToken)
    {
        return await connectionFactory.WithConnectionAsync(async connection =>
        {
            if (connection is not MySqlConnection mysql)
                throw new InvalidOperationException("Expected MySqlConnection.");

            await using var cmd = mysql.CreateCommand();
            cmd.CommandText = "SELECT type FROM chats WHERE chat_id = @chatId";
            cmd.Parameters.AddWithValue("@chatId", chatId);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            if (result is null)
                return null;

            return new ChatBaseInfo { ChatId = chatId, Type = (ChatType)Convert.ToSByte(result) };
        }, cancellationToken);
    }

    private async Task InvalidateUserChatsCacheAsync(int userId)
    {
        await _redis.KeyDeleteAsync($"minms:user:{userId}:chats");
    }

    private async Task InvalidateChatCacheAsync(int chatId)
    {
        // Удаляем все кэши для этого чата (по шаблону)
        var server = _redis.Multiplexer.GetServer(_redis.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"minms:chat:{chatId}:*");
        foreach (var key in keys)
            await _redis.KeyDeleteAsync(key);
    }

    private async Task InvalidateChatParticipantsCacheAsync(int chatId)
    {
        await _redis.KeyDeleteAsync($"minms:chat:{chatId}:*");
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
        catch (RedisException) { return default; }
        catch (JsonException) { return default; }
    }

    private async Task TrySetCachedAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        try
        {
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await _redis.StringSetAsync(key, payload, ttl ?? _cacheTtl).ConfigureAwait(false);
        }
        catch (RedisException) { }
    }

    private class ChatBaseInfo
    {
        public int ChatId { get; set; }
        public ChatType Type { get; set; }
    }
}
