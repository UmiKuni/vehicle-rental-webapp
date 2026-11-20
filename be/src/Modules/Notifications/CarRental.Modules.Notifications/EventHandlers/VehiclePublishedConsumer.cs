using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehiclePublished"/>.
/// </summary>
public sealed class VehiclePublishedConsumer(NotificationDeliveryService notifications)
{
    public async Task Consume(VehiclePublished @event, CancellationToken cancellationToken = default) =>
        await notifications.CreateAndDeliverAsync(
            @event.OwnerId,
            NotificationType.Push,
            $"Your vehicle {@event.Brand} {@event.Model} is now published.",
            $"vehicle-published:{@event.EventId}:{@event.VehicleId}",
            @event.OccurredAt,
            cancellationToken);
}

