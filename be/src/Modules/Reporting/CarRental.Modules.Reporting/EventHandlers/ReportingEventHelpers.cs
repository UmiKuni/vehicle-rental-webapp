using CarRental.BuildingBlocks.Redis;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;
using CarRental.Modules.Reporting.Services;

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
        CancellationToken ct,
        ICacheService? cacheService = null)
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
            await InvalidateReportingCacheAsync(cacheService, clock, occurredAt, projection, ct);
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
        CancellationToken ct,
        ICacheService? cacheService = null)
    {
        if (eventId == Guid.Empty)
            return;

        await ProjectionLock.WaitAsync(ct);
        try
        {
            if (await repository.HasEventBeenProcessedAsync(eventId, ct))
                return;

            BookingReportProjection? projection = null;
            if (bookingId != Guid.Empty)
            {
                projection = await repository.GetBookingProjectionAsync(bookingId, ct);
                if (projection is not null)
                    transition(projection);
            }

            repository.MarkEventProcessed(ProcessedEvent.Create(eventId, clock.GetUtcNow()));
            await dbContext.SaveChangesAsync(ct);
            await InvalidateReportingCacheAsync(cacheService, clock, null, projection, ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }

    public static async Task InvalidateReportingCacheAsync(
        ICacheService? cacheService,
        TimeProvider clock,
        DateTimeOffset? occurredAt,
        BookingReportProjection? projection,
        CancellationToken ct)
    {
        if (cacheService is null)
            return;

        var nowVn = clock.GetUtcNow().ToOffset(DailyBookingStats.PlatformTimeZoneOffset);
        var dashboardMonths = new HashSet<(int Year, int Month)> { (nowVn.Year, nowVn.Month) };
        if (occurredAt.HasValue)
        {
            var occurredVn = occurredAt.Value.ToOffset(DailyBookingStats.PlatformTimeZoneOffset);
            dashboardMonths.Add((occurredVn.Year, occurredVn.Month));
        }

        foreach (var (year, month) in dashboardMonths)
            await cacheService.RemoveAsync(ReportingCacheKeys.AdminDashboard(year, month), ct);

        if (projection is not null)
        {
            var startVn = projection.StartTime.ToOffset(DailyBookingStats.PlatformTimeZoneOffset);
            var ownerMonths = new HashSet<(int Year, int Month)>(dashboardMonths)
            {
                (startVn.Year, startVn.Month)
            };

            foreach (var (year, month) in ownerMonths)
                await cacheService.RemoveAsync(ReportingCacheKeys.OwnerRevenue(projection.OwnerId, year, month), ct);
        }
    }
}
