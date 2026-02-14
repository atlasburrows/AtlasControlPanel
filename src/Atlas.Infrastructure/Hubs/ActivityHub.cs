using Microsoft.AspNetCore.SignalR;

namespace Atlas.Infrastructure.Hubs;

public class ActivityHub : Hub
{
    public async Task SendActivityUpdate(string action, string description, string category)
        => await Clients.All.SendAsync("ActivityLogged", action, description, category);
}
