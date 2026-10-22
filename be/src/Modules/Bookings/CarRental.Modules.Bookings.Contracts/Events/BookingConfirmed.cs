using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingConfirmed : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Money PayableAmount { get; }
    public Money DepositPortion { get; }
    public DateTimeOffset PaymentDeadline { get; }

    public BookingConfirmed(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Money payableAmount,
        Money depositPortion,
        DateTimeOffset paymentDeadline)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        PayableAmount = payableAmount;
        DepositPortion = depositPortion;
        PaymentDeadline = paymentDeadline;
    }
}
