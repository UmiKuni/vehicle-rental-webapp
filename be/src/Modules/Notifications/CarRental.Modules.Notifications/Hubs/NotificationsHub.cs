using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CarRental.Modules.Notifications.Hubs;

/// <summary>
/// SignalR hub for real-time in-app notifications.
/// Clients connect authenticated and are automatically placed in their personal group
/// so the server can target them by user ID.
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        await base.OnConnectedAsync();
    }
}
