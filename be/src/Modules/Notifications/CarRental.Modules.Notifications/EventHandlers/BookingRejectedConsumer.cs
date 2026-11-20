using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRejected"/>.
/// </summary>
public sealed class BookingRejectedConsumer(
    NotificationDeliveryService notifications,
    IBookingApi bookings)
{
    public async Task Consume(BookingRejected @event, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetByIdAsync(@event.BookingId, Guid.Empty, true, cancellationToken);
        if (booking.IsFailure)
        {
            if (booking.Error.Code == "booking.not_found")
                return;
            throw new InvalidOperationException(booking.Error.Message);
        }

        await notifications.CreateAndDeliverAsync(
            booking.Value.CustomerId,
            NotificationType.Email,
            $"Your booking was rejected: {@event.Reason}",
            $"booking-rejected:{@event.EventId}:{booking.Value.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

