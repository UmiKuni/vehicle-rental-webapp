using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Notifications.Hubs;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.AspNetCore.SignalR;

namespace CarRental.Modules.Notifications.Services;

/// <summary>
/// Delivers a notification to the user's active browser/mobile connection(s)
/// via SignalR. If the user is offline the message is silently dropped —
/// the notification record is still persisted in the database.
/// </summary>
internal sealed class SignalRNotificationDeliveryProvider(
    IHubContext<NotificationsHub> hubContext)
{
    public Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct = default)
    {
        var payload = new
        {
            notificationId = notification.Id.Value,
            type           = notification.Type.ToString(),
            content        = notification.Content,
            occurredAt     = notification.CreatedAt
        };

        return hubContext
            .Clients
            .Group($"user:{notification.UserId}")
            .SendAsync("ReceiveNotification", payload, ct);
    }
}
