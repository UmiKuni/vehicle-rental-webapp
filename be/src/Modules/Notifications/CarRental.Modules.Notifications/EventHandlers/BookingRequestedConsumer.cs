using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRequested"/>.
/// </summary>
public sealed class BookingRequestedConsumer(NotificationDeliveryService notifications)
{
    public async Task Consume(BookingRequested @event, CancellationToken cancellationToken = default) =>
        await notifications.CreateAndDeliverAsync(
            @event.OwnerId,
            NotificationType.InApp,
            $"New booking request for vehicle {@event.VehicleId}.",
            $"inapp:booking-requested:{@event.EventId}:{@event.OwnerId}",
            @event.OccurredAt,
            cancellationToken);
}

