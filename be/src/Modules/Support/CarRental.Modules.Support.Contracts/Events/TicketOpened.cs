using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Support.Contracts.Events;

public sealed record TicketOpened : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid TicketId { get; }
    public Guid BookingId { get; }
    public Guid ReporterId { get; }

    public TicketOpened(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid ticketId,
        Guid bookingId,
        Guid reporterId)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        TicketId = ticketId;
        BookingId = bookingId;
        ReporterId = reporterId;
    }
}
