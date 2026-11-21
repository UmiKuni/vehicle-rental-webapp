namespace CarRental.Modules.Reporting.Contracts.Dtos;

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
