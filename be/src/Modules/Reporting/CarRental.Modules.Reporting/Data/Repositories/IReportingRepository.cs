using CarRental.Modules.Reporting.Entities;

namespace CarRental.Modules.Reporting.Data.Repositories;

public sealed record DashboardProjectionMetrics(
    int ActiveRentals,
    int PendingApprovalBookings,
    int CompletedTripsThisMonth);

public interface IReportingRepository
{
    Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken ct = default);

    void MarkEventProcessed(ProcessedEvent processedEvent);

    Task<DailyBookingStats?> GetDailyStatsAsync(DateOnly date, CancellationToken ct = default);

    void AddDailyStats(DailyBookingStats stats);

    Task<IReadOnlyList<DailyBookingStats>> GetDailyStatsRangeAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    Task<BookingReportProjection?> GetBookingProjectionAsync(Guid bookingId, CancellationToken ct = default);

    void AddBookingProjection(BookingReportProjection projection);

    Task<DashboardProjectionMetrics> GetDashboardMetricsAsync(
        DateTimeOffset monthStartUtc,
        DateTimeOffset monthEndUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<BookingReportProjection>> GetOwnerTripsInRangeAsync(
        Guid ownerId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken ct = default);
}
