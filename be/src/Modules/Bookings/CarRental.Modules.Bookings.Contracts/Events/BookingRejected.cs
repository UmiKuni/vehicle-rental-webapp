using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingRejected : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid OwnerId { get; }
    public string Reason { get; }

    public BookingRejected(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid ownerId,
        string reason)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        OwnerId = ownerId;
        Reason = reason;
    }
}
