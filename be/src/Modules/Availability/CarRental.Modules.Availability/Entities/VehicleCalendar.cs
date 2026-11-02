using CarRental.Modules.Availability.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts.Enums;

namespace CarRental.Modules.Availability.Entities;

public class VehicleCalendar : AggregateRoot<Guid>
{
    private static readonly TimeSpan MaxHoldTtl = TimeSpan.FromHours(12);
    private readonly List<CalendarEntry> _entries = new();

    public Guid VehicleId => Id;
    public DateTimeOffset LastSyncedAt { get; private set; }

    public IReadOnlyCollection<CalendarEntry> Entries => _entries.AsReadOnly();

    private VehicleCalendar() { }

    private VehicleCalendar(Guid vehicleId, DateTimeOffset now)
    {
        Id = vehicleId;
        LastSyncedAt = now;
    }

    public static VehicleCalendar Create(Guid vehicleId, DateTimeOffset now)
    {
        return new VehicleCalendar(vehicleId, now);
    }

    public Result PlaceHold(
        Guid bookingId,
        DateTimeRange range,
        TimeSpan ttl,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        if (range == null)
            return Result.Failure(AvailabilityErrors.InvalidRange);

        if (range.Start <= now)
            return Result.Failure(AvailabilityErrors.PastDate);

        if (!IsAvailable(range, now))
            return Result.Failure(AvailabilityErrors.SlotUnavailable3493D3);

        var effectiveTtl = ttl > MaxHoldTtl ? MaxHoldTtl : ttl;
        if (effectiveTtl <= TimeSpan.Zero)
            effectiveTtl = TimeSpan.FromHours(1);

        var expiresAt = now.Add(effectiveTtl);

        var entry = new CalendarEntry(
            Guid.CreateVersion7(),
            VehicleId,
            CalendarEntryType.Hold,
            range,
            expiresAt,
            bookingId);

        _entries.Add(entry);
        LastSyncedAt = now;

        return Result.Success();
    }

    public Result ConfirmHold(Guid bookingId, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        var holdEntry = _entries.FirstOrDefault(e => e.BookingId == bookingId && e.Type == CalendarEntryType.Hold);
        if (holdEntry == null || !holdEntry.IsActive(now))
            return Result.Failure(AvailabilityErrors.HoldNotFoundOrExpired);

        holdEntry.Confirm();
        LastSyncedAt = now;

        return Result.Success();
    }

    public Result Release(Guid bookingId, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure(AvailabilityErrors.InvalidBooking);

        _entries.RemoveAll(e => e.BookingId == bookingId);
        LastSyncedAt = now;

        return Result.Success();
    }

    public Result Block(CalendarEntryType type, DateTimeRange range, DateTimeOffset now)
    {
        if (type != CalendarEntryType.OwnerBlocked && type != CalendarEntryType.Maintenance)
            return Result.Failure(AvailabilityErrors.InvalidBlockType);

        if (range == null)
            return Result.Failure(AvailabilityErrors.InvalidRange);

        if (!IsAvailable(range, now))
            return Result.Failure(AvailabilityErrors.SlotUnavailableEA8C70);

        var entry = new CalendarEntry(
            Guid.CreateVersion7(),
            VehicleId,
            type,
            range,
            null,
            null);

        _entries.Add(entry);
        LastSyncedAt = now;

        return Result.Success();
    }

    public Result Unblock(Guid entryId, DateTimeOffset now)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == entryId);
        if (entry == null)
            return Result.Failure(AvailabilityErrors.EntryNotFound);

        if (entry.Type != CalendarEntryType.OwnerBlocked && entry.Type != CalendarEntryType.Maintenance)
            return Result.Failure(AvailabilityErrors.InvalidEntryType);

        _entries.Remove(entry);
        LastSyncedAt = now;

        return Result.Success();
    }

    public int ExpireHolds(DateTimeOffset now)
    {
        var expiredCount = _entries.RemoveAll(e => e.Type == CalendarEntryType.Hold && !e.IsActive(now));
        if (expiredCount > 0)
        {
            LastSyncedAt = now;
        }

        return expiredCount;
    }

    public bool IsAvailable(DateTimeRange range, DateTimeOffset now)
    {
        if (range == null) return false;

        return !_entries.Any(e => e.IsActive(now) && e.TimeRange.Overlaps(range));
    }
}
