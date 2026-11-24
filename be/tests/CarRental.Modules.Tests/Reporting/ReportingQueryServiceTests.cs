using System.Reflection;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Redis;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;
using CarRental.Modules.Reporting.Services;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using FluentAssertions;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Reporting;

public class ReportingQueryServiceTests
{
    // 2026-11-15 10:00:00 UTC -> 2026-11-15 17:00:00 UTC+7
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ReportingErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(DailyBookingStats).Assembly.GetType("CarRental.Modules.Reporting.Errors.ReportingErrors");
        errorsType.Should().NotBeNull();

        var fieldErrors = errorsType!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Name: f.Name, Error: (Error)f.GetValue(null)!));

        var propertyErrors = errorsType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Error))
            .Select(p => (Name: p.Name, Error: (Error)p.GetValue(null)!));

        var allErrors = fieldErrors.Concat(propertyErrors).ToList();
        allErrors.Should().HaveCount(5);

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex("^reporting\\.[a-z_]+$", $"Error '{name}' must use reporting.* snake_case naming.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();

        allErrors.Single(e => e.Name == "InvalidDateRange").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "RangeTooLarge").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "InvalidOwner").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "InvalidPeriod").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "ForbiddenOwnerAccess").Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task GetDashboardStatsAsync_ShouldAggregateUsersVehiclesProjectionsAndMonthlyStats()
    {
        var harness = CreateHarness();

        harness.UsersApiMock
            .Setup(x => x.GetAllUsersAsync(1, 1, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PagedResult<AdminUserDto>([], TotalCount: 42, Page: 1, PageSize: 1)));

        harness.FleetApiMock
            .Setup(x => x.GetPublishedVehiclesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<VehicleSummaryDto>>(
                [
                    CreateVehicleSummary(Guid.NewGuid(), Guid.NewGuid(), "51A-11111", "Mazda", "CX-5"),
                    CreateVehicleSummary(Guid.NewGuid(), Guid.NewGuid(), "51A-22222", "Honda", "Civic")
                ]));

        harness.RepositoryMock
            .Setup(x => x.GetDashboardMetricsAsync(
                new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardProjectionMetrics(ActiveRentals: 5, PendingApprovalBookings: 3, CompletedTripsThisMonth: 12));

        var day1 = DailyBookingStats.Create(new DateOnly(2026, 11, 1));
        day1.ApplyBookingRequested(new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero));
        day1.ApplyBookingRequested(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
        day1.ApplyPaymentSucceeded(2_500_000m, new DateTimeOffset(2026, 11, 1, 4, 0, 0, TimeSpan.Zero));

        var day2 = DailyBookingStats.Create(new DateOnly(2026, 11, 10));
        day2.ApplyBookingRequested(new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero));
        day2.ApplyBookingRequested(new DateTimeOffset(2026, 11, 10, 3, 0, 0, TimeSpan.Zero));
        day2.ApplyBookingCancelled(new DateTimeOffset(2026, 11, 10, 4, 0, 0, TimeSpan.Zero));
        day2.ApplyPaymentSucceeded(1_500_000m, new DateTimeOffset(2026, 11, 10, 5, 0, 0, TimeSpan.Zero));

        harness.RepositoryMock
            .Setup(x => x.GetDailyStatsRangeAsync(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), It.IsAny<CancellationToken>()))
            .ReturnsAsync([day1, day2]);

        var result = await harness.Service.GetDashboardStatsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUsers.Should().Be(42);
        result.Value.TotalVehicles.Should().Be(2);
        result.Value.ActiveRentals.Should().Be(5);
        result.Value.PendingApprovalBookings.Should().Be(3);
        result.Value.TotalRevenueThisMonth.Should().Be(4_000_000m);
        result.Value.TotalPlatformFeeCollected.Should().Be(400_000m);
        result.Value.CompletedTripsThisMonth.Should().Be(12);
        result.Value.CancellationRatePercentage.Should().Be(25.00m); // 1 cancelled / 4 bookings * 100
    }

    [Fact]
    public async Task GetDashboardStatsAsync_WhenUsersOrFleetApisFail_ShouldFallbackToZero()
    {
        var harness = CreateHarness();

        harness.UsersApiMock
            .Setup(x => x.GetAllUsersAsync(1, 1, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<PagedResult<AdminUserDto>>(Error.Failure("users.unavailable", "Down")));

        harness.FleetApiMock
            .Setup(x => x.GetPublishedVehiclesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient failure"));

        harness.RepositoryMock
            .Setup(x => x.GetDashboardMetricsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardProjectionMetrics(0, 0, 0));

        harness.RepositoryMock
            .Setup(x => x.GetDailyStatsRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await harness.Service.GetDashboardStatsAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUsers.Should().Be(0);
        result.Value.TotalVehicles.Should().Be(0);
        result.Value.CancellationRatePercentage.Should().Be(0m);
    }

    [Fact]
    public async Task GetDailyStatsAsync_ShouldFillMissingDatesWithZerosAndUseDefaultSevenDayRange()
    {
        // 2026-11-15 20:00:00 UTC -> 2026-11-16 03:00:00 UTC+7
        var lateUtcNow = new DateTimeOffset(2026, 11, 15, 20, 0, 0, TimeSpan.Zero);
        var harness = CreateHarness(lateUtcNow);

        var expectedStart = new DateOnly(2026, 11, 10);
        var expectedEnd = new DateOnly(2026, 11, 16);

        var statNov12 = DailyBookingStats.Create(new DateOnly(2026, 11, 12));
        statNov12.ApplyBookingRequested(new DateTimeOffset(2026, 11, 12, 2, 0, 0, TimeSpan.Zero));
        statNov12.ApplyPaymentSucceeded(3_000_000m, new DateTimeOffset(2026, 11, 12, 4, 0, 0, TimeSpan.Zero));

        harness.RepositoryMock
            .Setup(x => x.GetDailyStatsRangeAsync(expectedStart, expectedEnd, It.IsAny<CancellationToken>()))
            .ReturnsAsync([statNov12]);

        var result = await harness.Service.GetDailyStatsAsync(null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(7);
        result.Value.Select(d => d.Date).Should().ContainInOrder(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 11),
            new DateOnly(2026, 11, 12),
            new DateOnly(2026, 11, 13),
            new DateOnly(2026, 11, 14),
            new DateOnly(2026, 11, 15),
            new DateOnly(2026, 11, 16));

        result.Value[2].TotalBookings.Should().Be(1);
        result.Value[2].TotalRevenue.Should().Be(3_000_000m);
        result.Value[0].TotalBookings.Should().Be(0);
        result.Value[0].TotalRevenue.Should().Be(0m);
        result.Value[0].CancellationCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDailyStatsAsync_WithInvalidDateRangeOrRangeTooLarge_ShouldReturnValidationErrors()
    {
        var harness = CreateHarness();

        var invalidOrder = await harness.Service.GetDailyStatsAsync(
            new DateOnly(2026, 11, 10),
            new DateOnly(2026, 11, 5));

        invalidOrder.IsFailure.Should().BeTrue();
        invalidOrder.Error.Code.Should().Be("reporting.invalid_date_range");
        invalidOrder.Error.Type.Should().Be(ErrorType.Validation);

        var tooLarge = await harness.Service.GetDailyStatsAsync(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 2)); // 367 days inclusive (diff = 366 > 365)

        tooLarge.IsFailure.Should().BeTrue();
        tooLarge.Error.Code.Should().Be("reporting.range_too_large");
        tooLarge.Error.Type.Should().Be(ErrorType.Validation);

        // Exactly 366 days inclusive (diff = 365) should succeed
        harness.RepositoryMock
            .Setup(x => x.GetDailyStatsRangeAsync(new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var maxAllowed = await harness.Service.GetDailyStatsAsync(
            new DateOnly(2025, 1, 1),
            new DateOnly(2026, 1, 1));

        maxAllowed.IsSuccess.Should().BeTrue();
        maxAllowed.Value.Should().HaveCount(366);
    }

    [Fact]
    public async Task GetOwnerRevenueAsync_WithInvalidOwnerOrPeriod_ShouldReturnValidationErrors()
    {
        var harness = CreateHarness();

        var emptyOwner = await harness.Service.GetOwnerRevenueAsync(Guid.Empty, 11, 2026);
        emptyOwner.IsFailure.Should().BeTrue();
        emptyOwner.Error.Code.Should().Be("reporting.invalid_owner");
        emptyOwner.Error.Type.Should().Be(ErrorType.Validation);

        var invalidMonth = await harness.Service.GetOwnerRevenueAsync(Guid.NewGuid(), 13, 2026);
        invalidMonth.IsFailure.Should().BeTrue();
        invalidMonth.Error.Code.Should().Be("reporting.invalid_period");
        invalidMonth.Error.Type.Should().Be(ErrorType.Validation);

        var invalidYear = await harness.Service.GetOwnerRevenueAsync(Guid.NewGuid(), 5, 1999);
        invalidYear.IsFailure.Should().BeTrue();
        invalidYear.Error.Code.Should().Be("reporting.invalid_period");
        invalidYear.Error.Type.Should().Be(ErrorType.Validation);

        var zeroMonth = await harness.Service.GetOwnerRevenueAsync(Guid.NewGuid(), 0, 2026);
        zeroMonth.IsFailure.Should().BeTrue();
        zeroMonth.Error.Code.Should().Be("reporting.invalid_period");
        zeroMonth.Error.Type.Should().Be(ErrorType.Validation);

        var futureYear = await harness.Service.GetOwnerRevenueAsync(Guid.NewGuid(), 5, 2101);
        futureYear.IsFailure.Should().BeTrue();
        futureYear.Error.Code.Should().Be("reporting.invalid_period");
        futureYear.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task GetOwnerRevenueAsync_WhenMonthAndYearOmitted_ShouldUseVnCurrentMonthRange()
    {
        // 2026-10-31 18:00:00 UTC -> 2026-11-01 01:00:00 UTC+7 (November in VN)
        var boundaryUtc = new DateTimeOffset(2026, 10, 31, 18, 0, 0, TimeSpan.Zero);
        var harness = CreateHarness(boundaryUtc);
        var ownerId = Guid.NewGuid();

        harness.RepositoryMock
            .Setup(x => x.GetOwnerTripsInRangeAsync(
                ownerId,
                new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await harness.Service.GetOwnerRevenueAsync(ownerId, month: null, year: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Month.Should().Be(11);
        result.Value.Year.Should().Be(2026);
        result.Value.TotalTrips.Should().Be(0);

        harness.RepositoryMock.Verify(
            x => x.GetOwnerTripsInRangeAsync(
                ownerId,
                new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetOwnerRevenueAsync_ShouldComputeTenPercentCommissionAndNetEarnings()
    {
        var harness = CreateHarness();
        var ownerId = Guid.NewGuid();

        var trip1 = BookingReportProjection.Create(
            Guid.NewGuid(),
            ownerId,
            Guid.NewGuid(),
            "51K-889.99",
            "Mazda CX-5",
            new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 5, 13, 0, 0, TimeSpan.Zero),
            3_400_000m,
            new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        trip1.RecordPayment(3_400_000m, new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero));
        trip1.MarkCompleted(new DateTimeOffset(2026, 11, 5, 14, 0, 0, TimeSpan.Zero));

        var trip2 = BookingReportProjection.Create(
            Guid.NewGuid(),
            ownerId,
            Guid.NewGuid(),
            "51H-123.45",
            "Honda Civic",
            new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero),
            2_555m, // tests rounding: 255.5 -> 256 (ToEven)
            new DateTimeOffset(2026, 11, 9, 1, 0, 0, TimeSpan.Zero));
        trip2.MarkPaid(new DateTimeOffset(2026, 11, 9, 2, 0, 0, TimeSpan.Zero));

        harness.RepositoryMock
            .Setup(x => x.GetOwnerTripsInRangeAsync(
                ownerId,
                new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([trip1, trip2]);

        var result = await harness.Service.GetOwnerRevenueAsync(ownerId, 11, 2026);

        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerId.Should().Be(ownerId);
        result.Value.Month.Should().Be(11);
        result.Value.Year.Should().Be(2026);
        result.Value.TotalTrips.Should().Be(2);
        result.Value.Trips.Should().HaveCount(2);

        result.Value.Trips[0].GrossRent.Should().Be(3_400_000m);
        result.Value.Trips[0].PlatformCommission.Should().Be(340_000m);
        result.Value.Trips[0].NetIncome.Should().Be(3_060_000m);

        var expectedCommission2 = Math.Round(2_555m * 0.10m, 0);
        result.Value.Trips[1].PlatformCommission.Should().Be(expectedCommission2);
        result.Value.Trips[1].NetIncome.Should().Be(2_555m - expectedCommission2);

        result.Value.GrossRevenue.Should().Be(3_400_000m + 2_555m);
        result.Value.PlatformCommissionTotal.Should().Be(340_000m + expectedCommission2);
        result.Value.NetEarnings.Should().Be(result.Value.GrossRevenue - result.Value.PlatformCommissionTotal);
    }

    [Fact]
    public async Task GetOwnerRevenueAsync_WhenPartialRefundApplied_ShouldSubtractRefundAndClampAtZero()
    {
        var harness = CreateHarness();
        var ownerId = Guid.NewGuid();

        var partialRefundTrip = BookingReportProjection.Create(
            Guid.NewGuid(),
            ownerId,
            Guid.NewGuid(),
            "51K-889.99",
            "Mazda CX-5",
            new DateTimeOffset(2026, 11, 2, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 5, 13, 0, 0, TimeSpan.Zero),
            3_000_000m,
            new DateTimeOffset(2026, 11, 1, 1, 0, 0, TimeSpan.Zero));
        partialRefundTrip.RecordPayment(3_000_000m, new DateTimeOffset(2026, 11, 1, 2, 0, 0, TimeSpan.Zero));
        partialRefundTrip.ApplyRefund(1_000_000m);

        var overRefundTrip = BookingReportProjection.Create(
            Guid.NewGuid(),
            ownerId,
            Guid.NewGuid(),
            "51H-123.45",
            "Honda Civic",
            new DateTimeOffset(2026, 11, 10, 1, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero),
            1_500_000m,
            new DateTimeOffset(2026, 11, 9, 1, 0, 0, TimeSpan.Zero));
        overRefundTrip.RecordPayment(3_500_000m, new DateTimeOffset(2026, 11, 9, 2, 0, 0, TimeSpan.Zero));
        overRefundTrip.ApplyRefund(2_000_000m);

        harness.RepositoryMock
            .Setup(x => x.GetOwnerTripsInRangeAsync(
                ownerId,
                new DateTimeOffset(2026, 10, 31, 17, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 11, 30, 17, 0, 0, TimeSpan.Zero),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([partialRefundTrip, overRefundTrip]);

        var result = await harness.Service.GetOwnerRevenueAsync(ownerId, 11, 2026);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalTrips.Should().Be(2);
        result.Value.Trips[0].GrossRent.Should().Be(2_000_000m);
        result.Value.Trips[0].PlatformCommission.Should().Be(200_000m);
        result.Value.Trips[0].NetIncome.Should().Be(1_800_000m);

        result.Value.Trips[1].GrossRent.Should().Be(0m);
        result.Value.Trips[1].PlatformCommission.Should().Be(0m);
        result.Value.Trips[1].NetIncome.Should().Be(0m);

        result.Value.GrossRevenue.Should().Be(2_000_000m);
        result.Value.PlatformCommissionTotal.Should().Be(200_000m);
        result.Value.NetEarnings.Should().Be(1_800_000m);
    }

    private static (
        ReportingQueryService Service,
        Mock<IReportingRepository> RepositoryMock,
        Mock<IUsersApi> UsersApiMock,
        Mock<IFleetApi> FleetApiMock) CreateHarness(DateTimeOffset? utcNow = null)
    {
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(utcNow ?? Now);

        var repoMock = new Mock<IReportingRepository>();
        var usersApiMock = new Mock<IUsersApi>();
        var fleetApiMock = new Mock<IFleetApi>();

        var service = new ReportingQueryService(
            repoMock.Object,
            usersApiMock.Object,
            fleetApiMock.Object,
            clockMock.Object);

        return (service, repoMock, usersApiMock, fleetApiMock);
    }

    private static VehicleSummaryDto CreateVehicleSummary(
        Guid vehicleId,
        Guid ownerId,
        string plate,
        string brand,
        string model) =>
        new(
            vehicleId,
            ownerId,
            "Published",
            Guid.NewGuid(),
            brand,
            Guid.NewGuid(),
            model,
            plate,
            "Auto",
            5,
            null,
            GeoPoint.Create(10.7769, 106.7009).Value,
            "District 1, HCMC");

    [Fact]
    public async Task GetDashboardStatsAndOwnerRevenue_ShouldServeCachedValuesOnRepeatedCalls()
    {
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var repoMock = new Mock<IReportingRepository>();
        var usersApiMock = new Mock<IUsersApi>();
        var fleetApiMock = new Mock<IFleetApi>();
        var cache = new InMemoryCacheService(clockMock.Object);

        usersApiMock
            .Setup(x => x.GetAllUsersAsync(1, 1, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PagedResult<AdminUserDto>([], 5, 1, 1)));
        fleetApiMock
            .Setup(x => x.GetPublishedVehiclesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<VehicleSummaryDto>>([]));
        repoMock
            .Setup(x => x.GetDashboardMetricsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DashboardProjectionMetrics(1, 2, 3));
        repoMock
            .Setup(x => x.GetDailyStatsRangeAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ownerId = Guid.NewGuid();
        repoMock
            .Setup(x => x.GetOwnerTripsInRangeAsync(ownerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = new ReportingQueryService(
            repoMock.Object,
            usersApiMock.Object,
            fleetApiMock.Object,
            clockMock.Object,
            cache);

        var dash1 = await service.GetDashboardStatsAsync();
        var dash2 = await service.GetDashboardStatsAsync();
        dash1.IsSuccess.Should().BeTrue();
        dash2.Value.Should().BeEquivalentTo(dash1.Value);
        repoMock.Verify(x => x.GetDashboardMetricsAsync(It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);

        var rev1 = await service.GetOwnerRevenueAsync(ownerId, 11, 2026);
        var rev2 = await service.GetOwnerRevenueAsync(ownerId, 11, 2026);
        rev1.IsSuccess.Should().BeTrue();
        rev2.Value.Should().BeEquivalentTo(rev1.Value);
        repoMock.Verify(x => x.GetOwnerTripsInRangeAsync(ownerId, It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
