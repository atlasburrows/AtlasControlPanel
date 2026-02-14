using Microsoft.AspNetCore.SignalR;

namespace Atlas.Infrastructure.Hubs;

public class NotificationHub : Hub
{
    public async Task SendTaskUpdate(string taskId, string action)
        => await Clients.All.SendAsync("TaskUpdated", taskId, action);

    public async Task SendPermissionAlert(string requestId, string description)
        => await Clients.All.SendAsync("PermissionAlert", requestId, description);
}
