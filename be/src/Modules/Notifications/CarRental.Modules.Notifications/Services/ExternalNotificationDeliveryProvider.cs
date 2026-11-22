using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts.Dtos;

namespace CarRental.Modules.Notifications.Services;

internal sealed class ExternalNotificationDeliveryProvider(
    SmtpEmailDeliveryProvider emailProvider,
    TwilioSmsDeliveryProvider smsProvider,
    LoggingNotificationDeliveryProvider loggingProvider,
    SignalRNotificationDeliveryProvider signalRProvider) : INotificationDeliveryProvider
{
    public Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct = default) =>
        notification.Type switch
        {
            NotificationType.Email => emailProvider.DeliverAsync(notification, contact, ct),
            NotificationType.SMS   => smsProvider.DeliverAsync(notification, contact, ct),
            NotificationType.Push  => loggingProvider.DeliverAsync(notification, contact, ct),
            NotificationType.InApp => signalRProvider.DeliverAsync(notification, contact, ct),
            _ => throw new NotificationDeliveryException(
                $"Unsupported notification type: {notification.Type}")
        };
}
