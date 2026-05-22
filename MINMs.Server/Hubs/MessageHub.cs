using Microsoft.AspNetCore.SignalR;
using MINMs.Server.Services;
using MINMs.Server.Models.Dtos;
using Microsoft.AspNetCore.Authorization;

namespace MINMs.Server.Hubs;

[Authorize]
public class MessageHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUserSearchService _userSearchService;
    private readonly IContactService _contactService;
    private readonly IChatService _chatService;

    public MessageHub(
        IMessageService messageService,
        IUserSearchService userSearchService,
        IContactService contactService,
        IChatService chatService)
    {
        _messageService = messageService;
        _userSearchService = userSearchService;
        _contactService = contactService;
        _chatService = chatService;
    }

    public async Task SendMessageToChat(int chatId, string message, MessageType type = MessageType.Text)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var result = await _messageService.SendMessageAsync(
            userId.Value,
            chatId,
            message,
            type);

        switch (result)
        {
            case SendMessageResult.Success:
                var messages = await _messageService.GetMessagesAsync(userId.Value, chatId, 0, 1);
                var lastMessage = messages.FirstOrDefault();
                await Clients.Group($"chat_{chatId}").SendAsync("newMessage", new
                {
                    chatId,
                    messageId = lastMessage?.MessageId,
                    content = message,
                    senderId = userId.Value,
                    senderLogin = Context.User?.Identity?.Name,
                    createdAt = DateTime.UtcNow,
                    type = type
                });
                break;
            case SendMessageResult.AccessDenied:
                await Clients.Caller.SendAsync("error", "Access denied to this chat");
                break;
            case SendMessageResult.UserBlocked:
                await Clients.Caller.SendAsync("error", "You are blocked in this chat");
                break;
            case SendMessageResult.ContentTooLong:
                await Clients.Caller.SendAsync("error", "Message is too long (max 1000 characters)");
                break;
            default:
                await Clients.Caller.SendAsync("error", "Failed to send message");
                break;
        }
    }

    public async Task GetChatMessages(int chatId, int offset = 0, int limit = 50)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var messages = await _messageService.GetMessagesAsync(userId.Value, chatId, offset, limit);
        await Clients.Caller.SendAsync("chatMessages", messages);
    }

    public async Task DeleteMessage(int messageId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var result = await _messageService.DeleteMessageAsync(userId.Value, messageId);

        switch (result)
        {
            case DeleteMessageResult.Success:
                await Clients.Caller.SendAsync("messageDeleted", messageId);
                break;
            case DeleteMessageResult.NotFound:
                await Clients.Caller.SendAsync("error", "Message not found");
                break;
            case DeleteMessageResult.AccessDenied:
                await Clients.Caller.SendAsync("error", "You can only delete your own messages");
                break;
            default:
                await Clients.Caller.SendAsync("error", "Failed to delete message");
                break;
        }
    }

    public async Task JoinChatGroup(int chatId)
    {
        var userId = GetUserId();
        if (userId == null)
            return;

        var canAccess = await _messageService.CanUserAccessChatAsync(userId.Value, chatId);
        if (canAccess)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chatId}");

            var chat = await _chatService.GetChatByIdAsync(chatId, userId.Value);
            await Clients.Caller.SendAsync("joinedChat", new { chatId, chat });
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Cannot join this chat");
        }
    }

    public async Task LeaveChatGroup(int chatId)
    {
        var userId = GetUserId();
        if (userId == null)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");

        await _chatService.LeaveChatAsync(chatId, userId.Value);
        await Clients.Caller.SendAsync("leftChat", chatId);
    }

    public async Task SendMessageToUser(string userLogin, string message)
    {
        var senderId = GetUserId();
        if (senderId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var contacts = await _contactService.GetContactsAsync(senderId.Value);
        var contact = contacts.FirstOrDefault(c => c.Login == userLogin);

        if (contact == null)
        {
            await Clients.Caller.SendAsync("error", "User is not in your contacts");
            return;
        }

        var chatId = await GetOrCreatePrivateChat(senderId.Value, userLogin);
        if (chatId == null)
        {
            await Clients.Caller.SendAsync("error", "Failed to create or find chat");
            return;
        }

        await SendMessageToChat(chatId.Value, message);
    }

    public async Task CreateGroupChat(string chatName, List<string> participantLogins, string? initialMessage = null)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var participantIds = new List<int>();
        foreach (var login in participantLogins.Distinct())
        {
            if (login == Context.User?.Identity?.Name)
                continue;

            var user = await _userSearchService.GetByUserLoginAsync(login);
            if (user != null)
            {
                var userInternal = await _userSearchService.GetInternalByUserLoginAsync(login);
                if (userInternal != null)
                    participantIds.Add(userInternal.UserId);
            }
        }

        var request = new CreateChatRequest
        {
            Type = ChatType.Group,
            ParticipantIds = participantIds
        };

        var chat = await _chatService.CreateChatAsync(request, userId.Value);
        if (chat == null)
        {
            await Clients.Caller.SendAsync("error", "Failed to create group chat");
            return;
        }

        if (!string.IsNullOrWhiteSpace(initialMessage))
        {
            await SendMessageToChat(chat.ChatId, initialMessage);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chat.ChatId}");

        await Clients.Caller.SendAsync("groupChatCreated", chat);
    }

    public async Task AddParticipantToGroup(int chatId, string userLogin)
    {
        var ownerId = GetUserId();
        if (ownerId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var targetUser = await _userSearchService.GetInternalByUserLoginAsync(userLogin);
        if (targetUser == null)
        {
            await Clients.Caller.SendAsync("error", "User not found");
            return;
        }

        var result = await _chatService.AddParticipantAsync(chatId, ownerId.Value, targetUser.UserId);
        if (result)
        {
            await Clients.Caller.SendAsync("participantAdded", new { chatId, userLogin });
            await Clients.User(targetUser.UserId.ToString()).SendAsync("addedToChat", chatId);
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Failed to add participant");
        }
    }

    public async Task RemoveParticipantFromGroup(int chatId, string userLogin)
    {
        var ownerId = GetUserId();
        if (ownerId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var targetUser = await _userSearchService.GetInternalByUserLoginAsync(userLogin);
        if (targetUser == null)
        {
            await Clients.Caller.SendAsync("error", "User not found");
            return;
        }

        var result = await _chatService.RemoveParticipantAsync(chatId, ownerId.Value, targetUser.UserId);
        if (result)
        {
            await Clients.Caller.SendAsync("participantRemoved", new { chatId, userLogin });
            await Clients.User(targetUser.UserId.ToString()).SendAsync("removedFromChat", chatId);
            var connections = await GetUserConnections(targetUser.UserId.ToString());
            foreach (var connectionId in connections)
            {
                await Groups.RemoveFromGroupAsync(connectionId, $"chat_{chatId}");
            }
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Failed to remove participant");
        }
    }

    public async Task LeaveGroup(int chatId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var result = await _chatService.LeaveChatAsync(chatId, userId.Value);
        if (result)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
            await Clients.Caller.SendAsync("leftGroup", chatId);
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Failed to leave group");
        }
    }

    public async Task DeleteChat(int chatId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var result = await _chatService.DeleteChatAsync(chatId, userId.Value);
        if (result)
        {
            await Clients.Group($"chat_{chatId}").SendAsync("chatDeleted", chatId);
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Failed to delete chat or insufficient permissions");
        }
    }

    public async Task GetUserChats()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var chats = await _chatService.GetUserChatsAsync(userId.Value);
        await Clients.Caller.SendAsync("userChats", chats);
    }

    public async Task GetChatInfo(int chatId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            await Clients.Caller.SendAsync("error", "Not authenticated");
            return;
        }

        var chat = await _chatService.GetChatByIdAsync(chatId, userId.Value);
        if (chat == null)
        {
            await Clients.Caller.SendAsync("error", "Chat not found or access denied");
            return;
        }

        await Clients.Caller.SendAsync("chatInfo", chat);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var displayId = userId?.ToString() ?? Context.ConnectionId;

        if (userId.HasValue)
        {
            var chats = await _chatService.GetUserChatsAsync(userId.Value);
            foreach (var chat in chats)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chat.ChatId}");
            }
        }

        await Clients.Caller.SendAsync(
            "connected",
            $"Connected with ID: {displayId}"
        );

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId.HasValue)
        {
            var displayId = userId.ToString();
            await Clients.Caller.SendAsync("disconnected", $"Disconnected: {displayId}");
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int? GetUserId()
    {
        if (Context.UserIdentifier != null && int.TryParse(Context.UserIdentifier, out var userId))
            return userId;
        return null;
    }

    private async Task<int?> GetOrCreatePrivateChat(int userId, string targetUserLogin)
    {
        var targetUser = await _userSearchService.GetInternalByUserLoginAsync(targetUserLogin);
        if (targetUser == null)
            return null;

        var request = new CreateChatRequest
        {
            Type = ChatType.Personal,
            ParticipantIds = [targetUser.UserId]
        };

        var chat = await _chatService.CreateChatAsync(request, userId);

        if (chat != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{chat.ChatId}");

            var targetConnections = await GetUserConnections(targetUser.UserId.ToString());
            foreach (var connectionId in targetConnections)
            {
                await Groups.AddToGroupAsync(connectionId, $"chat_{chat.ChatId}");
            }
        }

        return chat?.ChatId;
    }

    private async Task<IReadOnlyList<string>> GetUserConnections(string userId)
    {
        return new List<string> { Context.ConnectionId };
    }
}
