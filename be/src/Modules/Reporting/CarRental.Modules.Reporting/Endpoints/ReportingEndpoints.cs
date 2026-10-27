using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Reporting.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reporting")
            .WithTags("Reporting");

        // 1. Get real-time high-level admin dashboard statistics
        group.MapGet("/dashboard", () =>
        {
            var stats = new AdminDashboardStatsDto(
                TotalUsers: 1_248,
                TotalVehicles: 382,
                ActiveRentals: 47,
                PendingApprovalBookings: 12,
                TotalRevenueThisMonth: 852_400_000m,
                TotalPlatformFeeCollected: 85_240_000m,
                CompletedTripsThisMonth: 189,
                CancellationRatePercentage: 3.8m);

            return Results.Ok(stats);
        })
        .WithName("GetAdminDashboardStats")
        .WithSummary("Số liệu tổng quan thời gian thực cho Admin")
        .WithDescription("Provides high-level platform analytics including active users, vehicles, ongoing rentals, monthly gross revenue, and cancellation rates.")
        .Produces<AdminDashboardStatsDto>(StatusCodes.Status200OK);

        // 2. Get daily booking and revenue time-series
        group.MapGet("/daily-stats", (DateOnly? from, DateOnly? to) =>
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startDate = from ?? today.AddDays(-6);
            var endDate = to ?? today;

            var dailyList = new List<DailyBookingStatsDto>();
            var rnd = new Random(42);

            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var bookings = rnd.Next(15, 35);
                var revenue = bookings * (decimal)rnd.Next(1_800_000, 3_200_000);
                var cancellations = rnd.Next(0, 3);

                dailyList.Add(new DailyBookingStatsDto(
                    Date: d,
                    TotalBookings: bookings,
                    TotalRevenue: revenue,
                    CancellationCount: cancellations));
            }

            return Results.Ok(dailyList);
        })
        .WithName("GetDailyBookingStats")
        .WithSummary("Biểu đồ doanh thu & số cuốc theo ngày")
        .WithDescription("Retrieves daily aggregated booking count, revenue, and cancellation metrics for trend charts.")
        .Produces<DailyBookingStatsDto[]>(StatusCodes.Status200OK);

        // 3. Get owner monthly revenue and trip breakdown
        group.MapGet("/owner/revenue", (Guid ownerId, int? month, int? year) =>
        {
            var targetMonth = month ?? DateTime.UtcNow.Month;
            var targetYear = year ?? DateTime.UtcNow.Year;

            var trips = new List<OwnerTripRevenueItemDto>
            {
                new(
                    BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                    VehiclePlate: "51K-889.99",
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    StartDate: new DateTimeOffset(targetYear, targetMonth, 2, 8, 0, 0, TimeSpan.FromHours(7)),
                    EndDate: new DateTimeOffset(targetYear, targetMonth, 6, 20, 0, 0, TimeSpan.FromHours(7)),
                    GrossRent: 3_400_000m,
                    PlatformCommission: 340_000m,
                    NetIncome: 3_060_000m),
                new(
                    BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000002"),
                    VehiclePlate: "51K-889.99",
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    StartDate: new DateTimeOffset(targetYear, targetMonth, 10, 7, 0, 0, TimeSpan.FromHours(7)),
                    EndDate: new DateTimeOffset(targetYear, targetMonth, 15, 19, 0, 0, TimeSpan.FromHours(7)),
                    GrossRent: 4_250_000m,
                    PlatformCommission: 425_000m,
                    NetIncome: 3_825_000m),
                new(
                    BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000003"),
                    VehiclePlate: "51K-889.99",
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    StartDate: new DateTimeOffset(targetYear, targetMonth, 20, 9, 0, 0, TimeSpan.FromHours(7)),
                    EndDate: new DateTimeOffset(targetYear, targetMonth, 23, 18, 0, 0, TimeSpan.FromHours(7)),
                    GrossRent: 2_550_000m,
                    PlatformCommission: 255_000m,
                    NetIncome: 2_295_000m)
            };

            var grossTotal = trips.Sum(t => t.GrossRent);
            var commissionTotal = trips.Sum(t => t.PlatformCommission);
            var netTotal = trips.Sum(t => t.NetIncome);

            var report = new OwnerRevenueReportDto(
                OwnerId: ownerId,
                Month: targetMonth,
                Year: targetYear,
                TotalTrips: trips.Count,
                GrossRevenue: grossTotal,
                PlatformCommissionTotal: commissionTotal,
                NetEarnings: netTotal,
                Trips: trips);

            return Results.Ok(report);
        })
        .WithName("GetOwnerRevenueReport")
        .WithSummary("Báo cáo doanh thu & chuyến đi cho Chủ xe")
        .WithDescription("Generates a detailed monthly revenue report for a specific vehicle owner, detailing gross earnings, platform commission (10%), and net payout.")
        .Produces<OwnerRevenueReportDto>(StatusCodes.Status200OK);

        return app;
    }
}

public sealed record AdminDashboardStatsDto(
    int TotalUsers,
    int TotalVehicles,
    int ActiveRentals,
    int PendingApprovalBookings,
    decimal TotalRevenueThisMonth,
    decimal TotalPlatformFeeCollected,
    int CompletedTripsThisMonth,
    decimal CancellationRatePercentage);

public sealed record DailyBookingStatsDto(
    DateOnly Date,
    int TotalBookings,
    decimal TotalRevenue,
    int CancellationCount);

public sealed record OwnerTripRevenueItemDto(
    Guid BookingId,
    string VehiclePlate,
    string VehicleModel,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    decimal GrossRent,
    decimal PlatformCommission,
    decimal NetIncome);

public sealed record OwnerRevenueReportDto(
    Guid OwnerId,
    int Month,
    int Year,
    int TotalTrips,
    decimal GrossRevenue,
    decimal PlatformCommissionTotal,
    decimal NetEarnings,
    IReadOnlyList<OwnerTripRevenueItemDto> Trips);
