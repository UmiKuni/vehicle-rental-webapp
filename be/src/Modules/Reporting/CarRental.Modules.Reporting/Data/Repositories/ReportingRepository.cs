using CarRental.Modules.Reporting.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Reporting.Data.Repositories;

internal sealed class ReportingRepository(ReportingDbContext db) : IReportingRepository
{
    public Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken ct = default) =>
        db.ProcessedEvents
            .AsNoTracking()
            .AnyAsync(e => e.Id == eventId, ct);

    public void MarkEventProcessed(ProcessedEvent processedEvent) =>
        db.ProcessedEvents.Add(processedEvent);

    public Task<DailyBookingStats?> GetDailyStatsAsync(DateOnly date, CancellationToken ct = default) =>
        db.DailyBookingStats
            .SingleOrDefaultAsync(s => s.Id == date, ct);

    public void AddDailyStats(DailyBookingStats stats) =>
        db.DailyBookingStats.Add(stats);

    public async Task<IReadOnlyList<DailyBookingStats>> GetDailyStatsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default) =>
        await db.DailyBookingStats
            .AsNoTracking()
            .Where(s => s.Id >= from && s.Id <= to)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

    public Task<BookingReportProjection?> GetBookingProjectionAsync(Guid bookingId, CancellationToken ct = default) =>
        db.BookingReportProjections
            .SingleOrDefaultAsync(p => p.Id == bookingId, ct);

    public void AddBookingProjection(BookingReportProjection projection) =>
        db.BookingReportProjections.Add(projection);

    public async Task<DashboardProjectionMetrics> GetDashboardMetricsAsync(
        DateTimeOffset monthStartUtc,
        DateTimeOffset monthEndUtc,
        CancellationToken ct = default)
    {
        var activeRentals = await db.BookingReportProjections
            .AsNoTracking()
            .CountAsync(p => p.Status == BookingProjectionStatus.InProgress, ct);

        var pendingBookings = await db.BookingReportProjections
            .AsNoTracking()
            .CountAsync(p => p.Status == BookingProjectionStatus.Pending, ct);

        var completedTrips = await db.BookingReportProjections
            .AsNoTracking()
            .CountAsync(
                p => p.Status == BookingProjectionStatus.Completed
                     && p.CompletedAt.HasValue
                     && p.CompletedAt.Value >= monthStartUtc
                     && p.CompletedAt.Value < monthEndUtc,
                ct);

        return new DashboardProjectionMetrics(activeRentals, pendingBookings, completedTrips);
    }

    public async Task<IReadOnlyList<BookingReportProjection>> GetOwnerTripsInRangeAsync(
        Guid ownerId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken ct = default) =>
        await db.BookingReportProjections
            .AsNoTracking()
            .Where(p => p.OwnerId == ownerId
                        && p.StartTime >= startUtc
                        && p.StartTime < endUtc
                        && p.Status != BookingProjectionStatus.Cancelled
                        && p.Status != BookingProjectionStatus.Rejected
                        && p.Status != BookingProjectionStatus.Expired
                        && (p.Status == BookingProjectionStatus.Paid
                            || p.Status == BookingProjectionStatus.InProgress
                            || p.Status == BookingProjectionStatus.Completed
                            || p.PaidAmount > 0m))
            .OrderBy(p => p.StartTime)
            .ToListAsync(ct);
}
