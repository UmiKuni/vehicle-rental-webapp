using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Bookings.Contracts.Enums;

namespace CarRental.Modules.Bookings.Entities;

public class BookingStatusHistory : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public BookingStatus Status { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public string? Reason { get; private set; }

    private BookingStatusHistory() { }

    public BookingStatusHistory(Guid id, Guid bookingId, BookingStatus status, DateTimeOffset changedAt, string? reason)
    {
        Id = id;
        BookingId = bookingId;
        Status = status;
        ChangedAt = changedAt;
        Reason = reason;
    }
}
