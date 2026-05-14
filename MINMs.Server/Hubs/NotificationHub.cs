using Microsoft.AspNetCore.SignalR;

namespace MINMs.Server.Hubs;

public class NotificationHub : Hub
{
    public async Task SendMessageToCaller(string message)
    {
        Console.WriteLine($"[SignalR] SendMessageToCaller called with message: {message ?? "null"}");
        await Clients.Caller.SendAsync("receiveMessage", message);
    }

    public async Task SendMessageToAll(string message)
    {
        await Clients.All.SendAsync("receiveMessage", message);
    }

    public async Task SendMessageToUser(string userId, string message)
    {
        await Clients.User(userId).SendAsync("receiveMessage", message);
    }

    public async Task Heartbeat()
    {
        await Clients.Caller.SendAsync("heartbeatResponse");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;

        await Clients.Caller.SendAsync(
            "connected",
            $"Connected with ID: {userId}"
        );

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}