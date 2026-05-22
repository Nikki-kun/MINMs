using Microsoft.AspNetCore.SignalR;
using MINMs.Server.Services;
using MINMs.Server.Models.Dtos;

namespace MINMs.Server.Hubs;

public class MessageHub : Hub
{
    private readonly IMessageService _messageService;
    private readonly IUserSearchService _userSearchService;
    private readonly IContactService _contactService;

    public MessageHub(
        IMessageService messageService,
        IUserSearchService userSearchService,
        IContactService contactService)
    {
        _messageService = messageService;
        _userSearchService = userSearchService;
        _contactService = contactService;
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
                await Clients.Group($"chat_{chatId}").SendAsync("newMessage", new { chatId, message, senderId = userId });
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
            await Clients.Caller.SendAsync("joinedChat", chatId);
        }
        else
        {
            await Clients.Caller.SendAsync("error", "Cannot join this chat");
        }
    }

    public async Task LeaveChatGroup(int chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{chatId}");
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
            await Clients.Caller.SendAsync("error", "Failed to create chat");
            return;
        }

        await SendMessageToChat(chatId.Value, message);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        var displayId = userId?.ToString() ?? Context.ConnectionId;

        await Clients.Caller.SendAsync(
            "connected",
            $"Connected with ID: {displayId}"
        );

        await base.OnConnectedAsync();
    }

    private int? GetUserId()
    {
        if (Context.UserIdentifier != null && int.TryParse(Context.UserIdentifier, out var userId))
            return userId;
        return null;
    }

    private async Task<int?> GetOrCreatePrivateChat(int userId, string targetUserLogin)
    {
        var targetUser = await _userSearchService.GetByUserLoginAsync(targetUserLogin);
        if (targetUser == null)
            return null;

        return 1;
    }
}
