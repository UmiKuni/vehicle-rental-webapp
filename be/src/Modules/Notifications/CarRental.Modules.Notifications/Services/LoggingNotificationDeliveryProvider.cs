using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Notifications.Services;

internal sealed class LoggingNotificationDeliveryProvider(
    ILogger<LoggingNotificationDeliveryProvider> logger) : INotificationDeliveryProvider
{
    public Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Delivered {NotificationType} notification {NotificationId} to user {UserId} ({Email})",
            notification.Type,
            notification.Id.Value,
            notification.UserId,
            contact?.Email ?? "unavailable");
        return Task.CompletedTask;
    }
}
