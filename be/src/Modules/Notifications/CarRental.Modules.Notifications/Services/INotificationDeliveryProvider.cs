using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts.Dtos;

namespace CarRental.Modules.Notifications.Services;

public interface INotificationDeliveryProvider
{
    Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct = default);
}
