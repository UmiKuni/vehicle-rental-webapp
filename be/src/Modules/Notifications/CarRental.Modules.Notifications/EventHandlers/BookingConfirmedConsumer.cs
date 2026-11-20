using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingConfirmed"/>.
/// </summary>
public sealed class BookingConfirmedConsumer(
    NotificationDeliveryService notifications,
    IBookingApi bookings)
{
    public async Task Consume(BookingConfirmed @event, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetByIdAsync(@event.BookingId, Guid.Empty, true, cancellationToken);
        if (booking.IsFailure)
            throw new InvalidOperationException(booking.Error.Message);

        await notifications.CreateAndDeliverAsync(
            booking.Value.CustomerId,
            NotificationType.Email,
            $"Booking confirmed. Payment is due by {@event.PaymentDeadline:O}.",
            $"booking-confirmed:{@event.EventId}:{booking.Value.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

