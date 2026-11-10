using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Support.Contracts.Events;

public sealed record TicketResolved : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid TicketId { get; }
    public Guid BookingId { get; }
    public int DepositToCustomerPercent { get; }
    public string Note { get; }
    public Guid AdminId { get; }

    public TicketResolved(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid ticketId,
        Guid bookingId,
        int depositToCustomerPercent,
        string note,
        Guid adminId)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        TicketId = ticketId;
        BookingId = bookingId;
        DepositToCustomerPercent = depositToCustomerPercent;
        Note = note;
        AdminId = adminId;
    }
}
