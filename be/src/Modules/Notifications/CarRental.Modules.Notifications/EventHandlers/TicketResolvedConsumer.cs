using CarRental.Modules.Support.Contracts.Events;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Services;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="TicketResolved"/>.
/// </summary>
public sealed class TicketResolvedConsumer(
    NotificationDeliveryService notifications,
    IBookingApi bookings)
{
    public async Task Consume(TicketResolved @event, CancellationToken cancellationToken = default)
    {
        var booking = await bookings.GetByIdAsync(@event.BookingId, Guid.Empty, true, cancellationToken);
        if (booking.IsFailure)
            throw new InvalidOperationException(booking.Error.Message);

        var content = $"Support ticket resolved: {@event.Note} " +
                      $"Deposit returned to customer: {@event.DepositToCustomerPercent}%.";
        await notifications.CreateAndDeliverAsync(
            booking.Value.CustomerId,
            NotificationType.Email,
            content,
            $"ticket-resolved:{@event.EventId}:{booking.Value.CustomerId}",
            @event.OccurredAt,
            cancellationToken);
        await notifications.CreateAndDeliverAsync(
            booking.Value.OwnerId,
            NotificationType.Email,
            content,
            $"ticket-resolved:{@event.EventId}:{booking.Value.OwnerId}",
            @event.OccurredAt,
            cancellationToken);
    }
}

