using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingCancelled : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid CustomerId { get; }
    public Money PenaltyAmount { get; }
    public Money RefundableAmount { get; }
    public string Reason { get; }

    public BookingCancelled(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid customerId,
        Money penaltyAmount,
        Money refundableAmount,
        string reason)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        CustomerId = customerId;
        PenaltyAmount = penaltyAmount;
        RefundableAmount = refundableAmount;
        Reason = reason;
    }
}
