using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentFailed"/>.
/// </summary>
public sealed class PaymentFailedConsumer(
    NotificationDeliveryService notifications,
    IBookingApi bookings)
{
    public async Task Consume(PaymentFailed @event, CancellationToken cancellationToken = default)
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
            $"Payment failed for booking {@event.BookingId}: {@event.Reason}",
            $"payment-failed:{@event.EventId}:{booking.Value.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

