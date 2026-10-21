using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Payments.Contracts.Events;

public sealed record PaymentSucceeded : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid PaymentId { get; }
    public Money Amount { get; }

    public PaymentSucceeded(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid paymentId,
        Money amount)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        PaymentId = paymentId;
        Amount = amount;
    }
}
