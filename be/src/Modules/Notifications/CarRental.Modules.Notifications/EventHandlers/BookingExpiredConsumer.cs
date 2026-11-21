using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingExpired"/>.
/// </summary>
public sealed class BookingExpiredConsumer(
    NotificationDeliveryService notifications,
    IBookingApi bookings)
{
    public async Task Consume(BookingExpired @event, CancellationToken cancellationToken = default)
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
            $"Booking expired: {@event.Reason}",
            $"booking-expired:{@event.EventId}:{booking.Value.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

