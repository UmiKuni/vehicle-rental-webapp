using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Reporting.Contracts;
using CarRental.Modules.Reporting.Contracts.Dtos;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;
using CarRental.Modules.Reporting.Errors;
using CarRental.Modules.Users.Contracts;

namespace CarRental.Modules.Reporting.Services;

internal sealed class ReportingQueryService(
    IReportingRepository repository,
    IUsersApi usersApi,
    IFleetApi fleetApi,
    TimeProvider clock) : IReportingQueries
{
    private const decimal PlatformCommissionRate = 0.10m;
    private const int MaxDailyStatsSpanDays = 365;

    public async Task<Result<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var vnNow = GetVnNow();
        var (monthStartUtc, monthEndUtc, monthStartDate, monthEndDate) = GetVnMonthRangeUtc(vnNow.Year, vnNow.Month);

        var totalUsers = await GetTotalUsersSafeAsync(ct);
        var totalVehicles = await GetTotalVehiclesSafeAsync(ct);
        var projectionMetrics = await repository.GetDashboardMetricsAsync(monthStartUtc, monthEndUtc, ct);
        var monthlyStats = await repository.GetDailyStatsRangeAsync(monthStartDate, monthEndDate, ct);

        var totalRevenueThisMonth = monthlyStats.Sum(s => s.TotalRevenue);
        var totalPlatformFee = totalRevenueThisMonth * PlatformCommissionRate;
        var totalBookings = monthlyStats.Sum(s => s.TotalBookings);
        var totalCancelled = monthlyStats.Sum(s => s.CancellationCount);
        var cancellationRate = totalBookings == 0
            ? 0m
            : Math.Round(totalCancelled * 100m / totalBookings, 2);

        return new AdminDashboardStatsDto(
            TotalUsers: totalUsers,
            TotalVehicles: totalVehicles,
            ActiveRentals: projectionMetrics.ActiveRentals,
            PendingApprovalBookings: projectionMetrics.PendingApprovalBookings,
            TotalRevenueThisMonth: totalRevenueThisMonth,
            TotalPlatformFeeCollected: totalPlatformFee,
            CompletedTripsThisMonth: projectionMetrics.CompletedTripsThisMonth,
            CancellationRatePercentage: cancellationRate);
    }

    public async Task<Result<IReadOnlyList<DailyBookingStatsDto>>> GetDailyStatsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        var rangeResult = ResolveAndValidateDateRange(from, to);
        if (rangeResult.IsFailure)
            return rangeResult.Error;

        var (startDate, endDate) = rangeResult.Value;
        var existingStats = await repository.GetDailyStatsRangeAsync(startDate, endDate, ct);

        return Result.Success<IReadOnlyList<DailyBookingStatsDto>>(
            BuildDailyStatsSeries(startDate, endDate, existingStats));
    }

    public async Task<Result<OwnerRevenueReportDto>> GetOwnerRevenueAsync(
        Guid ownerId,
        int? month,
        int? year,
        CancellationToken ct = default)
    {
        if (ownerId == Guid.Empty)
            return ReportingErrors.InvalidOwner;

        var periodResult = ValidatePeriod(month, year, GetVnNow());
        if (periodResult.IsFailure)
            return periodResult.Error;

        var (targetMonth, targetYear) = periodResult.Value;
        var (startUtc, endUtc, _, _) = GetVnMonthRangeUtc(targetYear, targetMonth);

        var projections = await repository.GetOwnerTripsInRangeAsync(ownerId, startUtc, endUtc, ct);
        var trips = projections.Select(MapTripRevenueItem).ToList();

        return new OwnerRevenueReportDto(
            OwnerId: ownerId,
            Month: targetMonth,
            Year: targetYear,
            TotalTrips: trips.Count,
            GrossRevenue: trips.Sum(t => t.GrossRent),
            PlatformCommissionTotal: trips.Sum(t => t.PlatformCommission),
            NetEarnings: trips.Sum(t => t.NetIncome),
            Trips: trips);
    }

    private DateTimeOffset GetVnNow() =>
        clock.GetUtcNow().ToOffset(DailyBookingStats.PlatformTimeZoneOffset);

    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtc, DateOnly StartDate, DateOnly EndDate)
        GetVnMonthRangeUtc(int year, int month)
    {
        var startVn = new DateTimeOffset(year, month, 1, 0, 0, 0, DailyBookingStats.PlatformTimeZoneOffset);
        var startDate = new DateOnly(year, month, 1);
        var endDate = startDate.AddMonths(1).AddDays(-1);
        return (startVn.ToUniversalTime(), startVn.AddMonths(1).ToUniversalTime(), startDate, endDate);
    }

    private Result<(DateOnly StartDate, DateOnly EndDate)> ResolveAndValidateDateRange(
        DateOnly? from,
        DateOnly? to)
    {
        var today = DailyBookingStats.ToBucketDate(clock.GetUtcNow());
        var startDate = from ?? today.AddDays(-6);
        var endDate = to ?? today;

        if (endDate < startDate)
            return ReportingErrors.InvalidDateRange;

        if (endDate.DayNumber - startDate.DayNumber > MaxDailyStatsSpanDays)
            return ReportingErrors.RangeTooLarge;

        return (startDate, endDate);
    }

    private static Result<(int Month, int Year)> ValidatePeriod(int? month, int? year, DateTimeOffset vnNow)
    {
        var targetMonth = month ?? vnNow.Month;
        var targetYear = year ?? vnNow.Year;

        if (targetMonth < 1 || targetMonth > 12 || targetYear < 2000 || targetYear > 2100)
            return ReportingErrors.InvalidPeriod;

        return (targetMonth, targetYear);
    }

    private static IReadOnlyList<DailyBookingStatsDto> BuildDailyStatsSeries(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<DailyBookingStats> existingStats)
    {
        var byDate = existingStats.ToDictionary(s => s.Date);
        var series = new List<DailyBookingStatsDto>(endDate.DayNumber - startDate.DayNumber + 1);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            series.Add(byDate.TryGetValue(date, out var stat)
                ? new DailyBookingStatsDto(stat.Date, stat.TotalBookings, stat.TotalRevenue, stat.CancellationCount)
                : new DailyBookingStatsDto(date, TotalBookings: 0, TotalRevenue: 0m, CancellationCount: 0));
        }

        return series;
    }

    private static OwnerTripRevenueItemDto MapTripRevenueItem(BookingReportProjection projection)
    {
        var grossRent = Math.Max(0m, projection.RentAmount - projection.RefundedAmount);
        var commission = Math.Round(grossRent * PlatformCommissionRate, 0);
        var netIncome = grossRent - commission;

        return new OwnerTripRevenueItemDto(
            BookingId: projection.Id,
            VehiclePlate: projection.VehiclePlate,
            VehicleModel: projection.VehicleModel,
            StartDate: projection.StartTime,
            EndDate: projection.EndTime,
            GrossRent: grossRent,
            PlatformCommission: commission,
            NetIncome: netIncome);
    }

    private async Task<int> GetTotalUsersSafeAsync(CancellationToken ct)
    {
        try
        {
            var result = await usersApi.GetAllUsersAsync(page: 1, pageSize: 1, ct: ct);
            return result.IsSuccess ? result.Value.TotalCount : 0;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return 0;
        }
    }

    private async Task<int> GetTotalVehiclesSafeAsync(CancellationToken ct)
    {
        try
        {
            var result = await fleetApi.GetPublishedVehiclesAsync(ct);
            return result.IsSuccess ? result.Value.Count : 0;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return 0;
        }
    }
}
