using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Contracts.Enums;

namespace CarRental.Modules.Availability.Entities;

public class CalendarEntry : Entity<Guid>
{
    public Guid VehicleId { get; private set; }
    public CalendarEntryType Type { get; private set; }
    public DateTimeRange TimeRange { get; private set; } = null!;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public Guid? BookingId { get; private set; }

    private CalendarEntry() { }

    public CalendarEntry(
        Guid id,
        Guid vehicleId,
        CalendarEntryType type,
        DateTimeRange timeRange,
        DateTimeOffset? expiresAt,
        Guid? bookingId)
    {
        Id = id;
        VehicleId = vehicleId;
        Type = type;
        TimeRange = timeRange;
        ExpiresAt = expiresAt;
        BookingId = bookingId;
    }

    public bool IsActive(DateTimeOffset now)
    {
        if (Type == CalendarEntryType.Hold)
        {
            return ExpiresAt.HasValue && ExpiresAt.Value > now;
        }

        return true;
    }

    internal void Confirm()
    {
        Type = CalendarEntryType.Booked;
        ExpiresAt = null;
    }
}
