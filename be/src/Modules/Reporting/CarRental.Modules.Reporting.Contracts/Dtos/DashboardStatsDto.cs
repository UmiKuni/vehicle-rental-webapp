namespace CarRental.Modules.Reporting.Contracts.Dtos;

/// <summary>
/// Data transfer object representing aggregated daily booking and revenue metrics for platform dashboards.
/// </summary>
public sealed record DashboardStatsDto(
    DateOnly Date,
    int TotalBookings,
    decimal TotalRevenue,
    int CancellationCount);
