using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingCancelled"/>.
/// </summary>
public sealed class BookingCancelledConsumer(NotificationDeliveryService notifications)
{
    public async Task Consume(BookingCancelled @event, CancellationToken cancellationToken = default)
    {
        await notifications.CreateAndDeliverAsync(
            @event.CustomerId,
            NotificationType.Email,
            $"Booking cancelled: {@event.Reason}. Refundable amount: {@event.RefundableAmount}.",
            $"booking-cancelled:{@event.EventId}:{@event.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
        await notifications.CreateAndDeliverAsync(
            @event.CustomerId,
            NotificationType.InApp,
            $"Booking cancelled: {@event.Reason}. Refundable amount: {@event.RefundableAmount}.",
            $"inapp:booking-cancelled:{@event.EventId}:{@event.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

