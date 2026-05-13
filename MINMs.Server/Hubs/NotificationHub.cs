using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace MINMs.Server.Hubs;

[Authorize]
public class NotificationHub : Hub
{

    public async Task SendMessageToCaller(string user, string message)
    {
        await Clients.Caller.SendAsync("ReceiveMessage", user, message);
    }
    
    public async Task SendMessageToAll(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
    
    public async Task SendMessageToUser(string userId, string user, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveMessage", user, message);
    }
    
    public async Task Heartbeat()
    {
        await Clients.Caller.SendAsync("HeartbeatResponse", "pong");
    }
    
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;
        await Clients.Caller.SendAsync("Connected", $"Connected with ID: {userId}");
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}