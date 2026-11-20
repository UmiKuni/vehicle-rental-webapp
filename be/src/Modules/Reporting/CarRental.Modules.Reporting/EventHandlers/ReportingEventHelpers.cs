using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;

namespace CarRental.Modules.Reporting.EventHandlers;

internal static class ReportingEventHelpers
{
    public static readonly SemaphoreSlim ProjectionLock = new(1, 1);

    public static async Task<DailyBookingStats> GetOrCreateDailyStatsAsync(
        DateTimeOffset occurredAt,
        IReportingRepository repository,
        CancellationToken ct)
    {
        var bucketDate = DailyBookingStats.ToBucketDate(occurredAt);
        var stats = await repository.GetDailyStatsAsync(bucketDate, ct);
        if (stats is not null)
            return stats;

        stats = DailyBookingStats.Create(bucketDate);
        repository.AddDailyStats(stats);
        return stats;
    }

    public static async Task HandleStatsAndProjectionAsync(
        Guid eventId,
        Guid bookingId,
        DateTimeOffset occurredAt,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        Action<DailyBookingStats> applyStats,
        Action<BookingReportProjection> applyProjection,
        CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return;

        await ProjectionLock.WaitAsync(ct);
        try
        {
            if (await repository.HasEventBeenProcessedAsync(eventId, ct))
                return;

            var stats = await GetOrCreateDailyStatsAsync(occurredAt, repository, ct);
            applyStats(stats);

            var projection = await repository.GetBookingProjectionAsync(bookingId, ct);
            if (projection is not null)
                applyProjection(projection);

            repository.MarkEventProcessed(ProcessedEvent.Create(eventId, clock.GetUtcNow()));
            await dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }

    public static async Task HandleProjectionTransitionAsync(
        Guid eventId,
        Guid bookingId,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        Action<BookingReportProjection> transition,
        CancellationToken ct)
    {
        if (eventId == Guid.Empty)
            return;

        await ProjectionLock.WaitAsync(ct);
        try
        {
            if (await repository.HasEventBeenProcessedAsync(eventId, ct))
                return;

            if (bookingId != Guid.Empty)
            {
                var projection = await repository.GetBookingProjectionAsync(bookingId, ct);
                if (projection is not null)
                    transition(projection);
            }

            repository.MarkEventProcessed(ProcessedEvent.Create(eventId, clock.GetUtcNow()));
            await dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }
}
