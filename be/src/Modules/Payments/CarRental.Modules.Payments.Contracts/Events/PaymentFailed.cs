using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Payments.Contracts.Events;

public sealed record PaymentFailed : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid PaymentId { get; }
    public string Reason { get; }

    public PaymentFailed(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid paymentId,
        string reason)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        PaymentId = paymentId;
        Reason = reason;
    }
}
