using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehicleUnpublished"/>.
/// </summary>
public sealed class VehicleUnpublishedConsumer(NotificationDeliveryService notifications)
{
    public async Task Consume(VehicleUnpublished @event, CancellationToken cancellationToken = default)
    {
        await notifications.CreateAndDeliverAsync(
            @event.OwnerId,
            NotificationType.Email,
            $"Your vehicle listing was unpublished: {@event.Reason}",
            $"vehicle-unpublished:{@event.EventId}:{@event.VehicleId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

