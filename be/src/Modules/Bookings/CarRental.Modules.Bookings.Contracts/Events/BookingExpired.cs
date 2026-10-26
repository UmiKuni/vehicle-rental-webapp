using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingExpired : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public string Reason { get; }

    public BookingExpired(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        string reason)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        Reason = reason;
    }
}
