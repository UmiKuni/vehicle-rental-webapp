using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Availability.Data.Repositories;

internal sealed class VehicleCalendarRepository : IVehicleCalendarRepository
{
    private readonly AvailabilityDbContext _dbContext;

    public VehicleCalendarRepository(AvailabilityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<VehicleCalendar?> GetByVehicleIdAsync(Guid vehicleId, CancellationToken ct = default) =>
        _dbContext.VehicleCalendars
            .Include(c => c.Entries)
            .SingleOrDefaultAsync(c => c.Id == vehicleId, ct);

    public Task<VehicleCalendar?> GetByEntryIdAsync(Guid entryId, CancellationToken ct = default) =>
        _dbContext.VehicleCalendars
            .Include(c => c.Entries)
            .SingleOrDefaultAsync(c => c.Entries.Any(e => e.Id == entryId), ct);

    public Task<VehicleCalendar?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        _dbContext.VehicleCalendars
            .Include(c => c.Entries)
            .FirstOrDefaultAsync(c => c.Entries.Any(e => e.BookingId == bookingId), ct);

    public Task<bool> HasActiveOverlapAsync(
        Guid vehicleId,
        DateTimeRange range,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        _dbContext.CalendarEntries
            .AsNoTracking()
            .AnyAsync(
                e => e.VehicleId == vehicleId
                     && (e.Type != CalendarEntryType.Hold || (e.ExpiresAt.HasValue && e.ExpiresAt.Value > now))
                     && e.TimeRange.Start < range.End
                     && e.TimeRange.End > range.Start,
                ct);

    public async Task<IReadOnlyList<CalendarEntry>> GetActiveOverlappingEntriesAsync(
        Guid vehicleId,
        DateTimeRange range,
        DateTimeOffset now,
        CancellationToken ct = default) =>
        await _dbContext.CalendarEntries
            .AsNoTracking()
            .Where(e => e.VehicleId == vehicleId
                        && (e.Type != CalendarEntryType.Hold || (e.ExpiresAt.HasValue && e.ExpiresAt.Value > now))
                        && e.TimeRange.Start < range.End
                        && e.TimeRange.End > range.Start)
            .OrderBy(e => e.TimeRange.Start)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CalendarEntry>> GetActiveEntriesAsync(
        Guid vehicleId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var query = _dbContext.CalendarEntries
            .AsNoTracking()
            .Where(e => e.VehicleId == vehicleId
                        && (e.Type != CalendarEntryType.Hold || (e.ExpiresAt.HasValue && e.ExpiresAt.Value > now)));

        if (from.HasValue)
            query = query.Where(e => e.TimeRange.End > from.Value);

        if (to.HasValue)
            query = query.Where(e => e.TimeRange.Start < to.Value);

        return await query
            .OrderBy(e => e.TimeRange.Start)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VehicleCalendar>> GetCalendarsWithExpiredHoldsAsync(
        DateTimeOffset now,
        CancellationToken ct = default) =>
        await _dbContext.VehicleCalendars
            .Include(c => c.Entries)
            .Where(c => c.Entries.Any(e =>
                e.Type == CalendarEntryType.Hold
                && (!e.ExpiresAt.HasValue || e.ExpiresAt.Value <= now)))
            .ToListAsync(ct);

    public void Add(VehicleCalendar calendar) =>
        _dbContext.VehicleCalendars.Add(calendar);
}
