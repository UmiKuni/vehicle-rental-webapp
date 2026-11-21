using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reporting.Contracts.Dtos;

namespace CarRental.Modules.Reporting.Contracts;

/// <summary>
/// Read-only query contract for platform analytics and owner revenue reporting.
/// </summary>
public interface IReportingQueries
{
    Task<Result<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken ct = default);

    Task<Result<IReadOnlyList<DailyBookingStatsDto>>> GetDailyStatsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default);

    Task<Result<OwnerRevenueReportDto>> GetOwnerRevenueAsync(
        Guid ownerId,
        int? month,
        int? year,
        CancellationToken ct = default);
}
