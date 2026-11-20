using System.Net;
using System.Net.Http.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Reporting.Contracts.Dtos;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Reporting;

public class ReportingIntegrationTests : IntegrationTestBase
{
    public ReportingIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVehicleAsync(Guid ownerId, string plateNumber)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var brand = new VehicleBrand(Guid.NewGuid(), "Mazda");
        var model = new VehicleModel(Guid.NewGuid(), brand.Id, "CX-5");
        var vehicle = Vehicle.Create(
            ownerId,
            plateNumber,
            brand.Id,
            model.Id,
            TransmissionType.Auto,
            5,
            new GeoPoint(10.7769, 106.7009),
            "District 1, HCMC").Value;

        db.VehicleBrands.Add(brand);
        db.VehicleModels.Add(model);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        return vehicle.Id.Value;
    }

    private async Task WaitForConditionAsync(Func<ReportingDbContext, Task<bool>> predicate)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
            if (await predicate(db))
                return;

            await Task.Delay(150);
        }

        throw new TimeoutException("Timed out waiting for reporting projection to update.");
    }

    [Fact]
    public async Task EventDriven_BookingRequested_PaymentSucceeded_And_BookingCancelled_ShouldProjectToDatabaseAndEndpoints()
    {
        var (adminClient, _) = await CreateAuthenticatedAdminClientAsync(
            "admin_reporting@example.com",
            "0901112233");
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_reporting@example.com",
            "0902223344",
            taxCode: "0102030499");
        var (_, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "customer_reporting@example.com",
            "0903334455");

        var vehicleId = await SeedVehicleAsync(ownerId, "51K-889.99");

        var now = DateTimeOffset.UtcNow;
        var vnNow = now.ToOffset(DailyBookingStats.PlatformTimeZoneOffset);
        var todayBucket = DailyBookingStats.ToBucketDate(now);

        var paidBookingId = Guid.NewGuid();
        var cancelledBookingId = Guid.NewGuid();

        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            await bus.PublishAsync(new BookingRequested(
                Guid.NewGuid(),
                now,
                paidBookingId,
                customerId,
                ownerId,
                vehicleId,
                DateTimeRange.Create(now.AddHours(2), now.AddDays(2)).Value,
                new Money(3_000_000L, "VND"),
                new Money(1_000_000L, "VND")));
        }

        await WaitForConditionAsync(db =>
            db.BookingReportProjections.AsNoTracking().AnyAsync(p => p.Id == paidBookingId));

        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            await bus.PublishAsync(new PaymentSucceeded(
                Guid.NewGuid(),
                now,
                paidBookingId,
                Guid.NewGuid(),
                new Money(3_000_000L, "VND")));

            await bus.PublishAsync(new BookingRequested(
                Guid.NewGuid(),
                now,
                cancelledBookingId,
                customerId,
                ownerId,
                vehicleId,
                DateTimeRange.Create(now.AddDays(4), now.AddDays(6)).Value,
                new Money(1_500_000L, "VND"),
                new Money(500_000L, "VND")));
        }

        await WaitForConditionAsync(async db =>
            await db.BookingReportProjections.AsNoTracking().AnyAsync(p =>
                p.Id == paidBookingId && p.PaidAmount == 3_000_000m)
            && await db.BookingReportProjections.AsNoTracking().AnyAsync(p =>
                p.Id == cancelledBookingId));

        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            await bus.PublishAsync(new BookingCancelled(
                Guid.NewGuid(),
                now,
                cancelledBookingId,
                customerId,
                new Money(0L, "VND"),
                new Money(0L, "VND"),
                "Customer cancelled"));
        }

        await WaitForConditionAsync(db =>
            db.BookingReportProjections.AsNoTracking().AnyAsync(p =>
                p.Id == cancelledBookingId && p.Status == BookingProjectionStatus.Cancelled));

        // 1. Dashboard endpoint
        var dashboardResponse = await adminClient.GetAsync("/api/reporting/dashboard");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.Content.ReadFromJsonAsync<AdminDashboardStatsDto>();
        dashboard.Should().NotBeNull();
        dashboard!.TotalUsers.Should().BeGreaterThanOrEqualTo(3);
        dashboard.TotalRevenueThisMonth.Should().Be(3_000_000m);
        dashboard.TotalPlatformFeeCollected.Should().Be(300_000m);
        dashboard.CancellationRatePercentage.Should().Be(50.00m);

        // 2. Daily stats endpoint
        var dailyResponse = await adminClient.GetAsync(
            $"/api/reporting/daily-stats?from={todayBucket:yyyy-MM-dd}&to={todayBucket:yyyy-MM-dd}");
        dailyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dailyStats = await dailyResponse.Content.ReadFromJsonAsync<IReadOnlyList<DailyBookingStatsDto>>();
        dailyStats.Should().ContainSingle();
        dailyStats![0].Date.Should().Be(todayBucket);
        dailyStats[0].TotalBookings.Should().Be(2);
        dailyStats[0].TotalRevenue.Should().Be(3_000_000m);
        dailyStats[0].CancellationCount.Should().Be(1);

        // 3. Owner revenue endpoint
        var ownerRevenueResponse = await ownerClient.GetAsync(
            $"/api/reporting/owner/revenue?ownerId={ownerId}&month={vnNow.Month}&year={vnNow.Year}");
        ownerRevenueResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerReport = await ownerRevenueResponse.Content.ReadFromJsonAsync<OwnerRevenueReportDto>();
        ownerReport.Should().NotBeNull();
        ownerReport!.OwnerId.Should().Be(ownerId);
        ownerReport.TotalTrips.Should().Be(1);
        ownerReport.GrossRevenue.Should().Be(3_000_000m);
        ownerReport.PlatformCommissionTotal.Should().Be(300_000m);
        ownerReport.NetEarnings.Should().Be(2_700_000m);
        ownerReport.Trips.Should().ContainSingle(t =>
            t.BookingId == paidBookingId
            && t.VehiclePlate == "51K-889.99"
            && t.VehicleModel == "Mazda CX-5");

        // 4. PaymentRefunded deducts revenue & paid amount
        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new PaymentRefunded(
                Guid.NewGuid(),
                now,
                paidBookingId,
                Guid.NewGuid(),
                new Money(1_000_000L, "VND"),
                IsFullRefund: false,
                "Partial refund test"));
        }

        await WaitForConditionAsync(async db =>
        {
            var proj = await db.BookingReportProjections.AsNoTracking().SingleAsync(p => p.Id == paidBookingId);
            var stat = await db.DailyBookingStats.AsNoTracking().SingleAsync(s => s.Id == todayBucket);
            return proj.PaidAmount == 2_000_000m && stat.TotalRevenue == 2_000_000m;
        });
    }

    [Fact]
    public async Task ReportingEndpoints_ShouldEnforceAuthorizationAndValidation()
    {
        var (adminClient, _) = await CreateAuthenticatedAdminClientAsync(
            "admin_reporting_auth@example.com",
            "0904445566");
        var (ownerAClient, ownerAId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_reporting_a@example.com",
            "0905556677",
            taxCode: "0102030491");
        var (_, ownerBId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_reporting_b@example.com",
            "0906667788",
            taxCode: "0102030492");
        var (customerClient, _) = await CreateAuthenticatedCustomerClientAsync(
            "customer_reporting_auth@example.com",
            "0907778899");

        // Unauthenticated -> 401
        (await Client.GetAsync("/api/reporting/dashboard")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync("/api/reporting/daily-stats")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.GetAsync($"/api/reporting/owner/revenue?ownerId={ownerAId}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Customer -> 403
        (await customerClient.GetAsync("/api/reporting/dashboard")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customerClient.GetAsync("/api/reporting/daily-stats")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customerClient.GetAsync($"/api/reporting/owner/revenue?ownerId={ownerAId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Owner A querying Owner B -> 403
        (await ownerAClient.GetAsync($"/api/reporting/owner/revenue?ownerId={ownerBId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Owner A querying without ownerId -> 200 for self
        var selfResponse = await ownerAClient.GetAsync("/api/reporting/owner/revenue");
        selfResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var selfReport = await selfResponse.Content.ReadFromJsonAsync<OwnerRevenueReportDto>();
        selfReport!.OwnerId.Should().Be(ownerAId);

        // Admin querying Owner B -> 200
        var adminOwnerResponse = await adminClient.GetAsync($"/api/reporting/owner/revenue?ownerId={ownerBId}");
        adminOwnerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Admin invalid date range -> 400
        var invalidRangeResponse = await adminClient.GetAsync("/api/reporting/daily-stats?from=2026-11-10&to=2026-11-05");
        invalidRangeResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
