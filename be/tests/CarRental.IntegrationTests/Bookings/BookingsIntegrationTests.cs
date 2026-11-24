using System.Net;
using System.Net.Http.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Pricing.Contracts.Dtos;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Bookings;

public class BookingsIntegrationTests : IntegrationTestBase
{
    public BookingsIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedPublishedVehicleWithPricingAsync(
        HttpClient ownerClient,
        Guid ownerId,
        string plateNumber)
    {
        Guid vehicleId;
        using (var scope = CreateScope())
        {
            var fleetDb = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

            var brand = new VehicleBrand(Guid.NewGuid(), "Toyota");
            var model = new VehicleModel(Guid.NewGuid(), brand.Id, "Camry");
            var vehicle = Vehicle.Create(
                ownerId,
                plateNumber,
                brand.Id,
                model.Id,
                TransmissionType.Auto,
                5,
                new GeoPoint(10.7769, 106.7009),
                "District 1, HCMC").Value;

            vehicle.AddImage("vehicles/img1.jpg");
            vehicle.AddImage("vehicles/img2.jpg");
            vehicle.AddImage("vehicles/img3.jpg");
            vehicle.SubmitForApproval().IsSuccess.Should().BeTrue();
            vehicle.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

            fleetDb.VehicleBrands.Add(brand);
            fleetDb.VehicleModels.Add(model);
            fleetDb.Vehicles.Add(vehicle);
            await fleetDb.SaveChangesAsync();

            vehicleId = vehicle.Id.Value;
        }

        var pricingResponse = await ownerClient.PutAsJsonAsync(
            $"/api/pricing/vehicles/{vehicleId}",
            new ConfigurePricingDto(
                HourlyRate: null,
                WeekdayRate: new Money(800_000, "VND"),
                WeekendRate: new Money(1_000_000, "VND"),
                DepositAmount: new Money(5_000_000, "VND"),
                KmLimit: 300,
                OverKmFee: new Money(5_000, "VND"),
                CleaningFee: new Money(100_000, "VND"),
                OwnerId: Guid.Empty));
        pricingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        return vehicleId;
    }

    [Fact]
    public async Task Create_GetMy_GetOwner_And_ApproveBooking_ShouldPersistAndKeepHold()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_book_1@example.com",
            "0901110001",
            taxCode: "0102030501");
        var (customerClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_book_1@example.com",
            "0901110002");

        var vehicleId = await SeedPublishedVehicleWithPricingAsync(ownerClient, ownerId, "51A-700.01");

        var start = DateTimeOffset.UtcNow.AddDays(3);
        var end = DateTimeOffset.UtcNow.AddDays(5);

        // 1. Create booking
        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest(vehicleId, start, end, "123 Nguyen Hue, D1, HCMC", false, null));

        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<BookingDetailDto>();
        created.Should().NotBeNull();
        created!.CustomerId.Should().Be(customerId);
        created.OwnerId.Should().Be(ownerId);
        created.VehicleId.Should().Be(vehicleId);
        created.Status.Should().Be(nameof(BookingStatus.Pending));
        created.SecurityDepositAmount.Should().Be(5_000_000m);
        created.TotalRentAmount.Should().BeGreaterThan(0);

        // 2. Overlapping booking request returns 409 Conflict due to availability hold
        var overlapResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest(vehicleId, start.AddHours(6), end.AddHours(6), "District 3, HCMC", false, null));
        overlapResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. GetMyBookings
        var myResponse = await customerClient.GetAsync("/api/bookings/me?status=Pending&page=1&pageSize=10");
        myResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var myBookings = await myResponse.Content.ReadFromJsonAsync<PagedResult<BookingSummaryDto>>();
        myBookings.Should().NotBeNull();
        myBookings!.Items.Should().ContainSingle(b => b.Id == created.Id);

        // 4. GetOwnerBookings
        var ownerListResponse = await ownerClient.GetAsync("/api/bookings/owner?status=Pending&page=1&pageSize=10");
        ownerListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownerBookings = await ownerListResponse.Content.ReadFromJsonAsync<PagedResult<BookingSummaryDto>>();
        ownerBookings.Should().NotBeNull();
        ownerBookings!.Items.Should().ContainSingle(b => b.Id == created.Id);

        // 5. Approve booking as owner
        var approveResponse = await ownerClient.PutAsync($"/api/bookings/{created.Id}/approve", null);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approveResponse.Content.ReadFromJsonAsync<BookingDetailDto>();
        approved.Should().NotBeNull();
        approved!.Status.Should().Be(nameof(BookingStatus.Confirmed));
        approved.HoldExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RejectBooking_AsOwner_ShouldTransitionToRejectedAndReleaseAvailabilityHold()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_book_2@example.com",
            "0901110003",
            taxCode: "0102030502");
        var (customerClient, _) = await CreateAuthenticatedCustomerClientAsync(
            "cust_book_2@example.com",
            "0901110004");

        var vehicleId = await SeedPublishedVehicleWithPricingAsync(ownerClient, ownerId, "51A-700.02");
        var start = DateTimeOffset.UtcNow.AddDays(4);
        var end = DateTimeOffset.UtcNow.AddDays(6);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest(vehicleId, start, end, "District 1, HCMC", false, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<BookingDetailDto>())!;

        var rejectResponse = await ownerClient.PutAsJsonAsync(
            $"/api/bookings/{created.Id}/reject",
            new RejectBookingRequest("Vehicle requires scheduled maintenance"));
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var rejected = await rejectResponse.Content.ReadFromJsonAsync<BookingDetailDto>();
        rejected.Should().NotBeNull();
        rejected!.Status.Should().Be(nameof(BookingStatus.Rejected));

        using var scope = CreateScope();
        var availabilityApi = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
        var available = await availabilityApi.CheckAvailabilityAsync(vehicleId, new DateTimeRange(start, end));
        available.IsSuccess.Should().BeTrue();
        available.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CancelBooking_AsCustomer_ShouldTransitionToCancelledAndReleaseAvailabilityHold()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_book_3@example.com",
            "0901110005",
            taxCode: "0102030503");
        var (customerClient, _) = await CreateAuthenticatedCustomerClientAsync(
            "cust_book_3@example.com",
            "0901110006");

        var vehicleId = await SeedPublishedVehicleWithPricingAsync(ownerClient, ownerId, "51A-700.03");
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var end = DateTimeOffset.UtcNow.AddDays(7);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest(vehicleId, start, end, "District 7, HCMC", false, null));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await createResponse.Content.ReadFromJsonAsync<BookingDetailDto>())!;

        var cancelResponse = await customerClient.PutAsJsonAsync(
            $"/api/bookings/{created.Id}/cancel",
            new CancelBookingRequest("Schedule changed"));
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<BookingCancellationResultDto>();
        cancelled.Should().NotBeNull();
        cancelled!.BookingId.Should().Be(created.Id);
        cancelled.Status.Should().Be(nameof(BookingStatus.Cancelled));
        cancelled.PenaltyAmount.Should().Be(0m);
        cancelled.RefundAmount.Should().Be(0m);

        using var scope = CreateScope();
        var availabilityApi = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
        var available = await availabilityApi.CheckAvailabilityAsync(vehicleId, new DateTimeRange(start, end));
        available.IsSuccess.Should().BeTrue();
        available.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CreateBookingsConcurrently_OverlappingShouldAllowOnlyOne_NonOverlappingShouldAllowBoth()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_book_race@example.com",
            "0901110099",
            taxCode: "0102030599");
        var (customer1Client, _) = await CreateAuthenticatedCustomerClientAsync(
            "cust_book_race_1@example.com",
            "0901110091");
        var (customer2Client, _) = await CreateAuthenticatedCustomerClientAsync(
            "cust_book_race_2@example.com",
            "0901110092");

        var vehicleId = await SeedPublishedVehicleWithPricingAsync(ownerClient, ownerId, "51A-700.99");

        // 1. Concurrent overlapping requests for the same vehicle -> exactly one 201 and one 409
        var overlapStart = DateTimeOffset.UtcNow.AddDays(10);
        var overlapEnd = overlapStart.AddDays(2);

        var overlapTasks = new[]
        {
            customer1Client.PostAsJsonAsync(
                "/api/bookings",
                new CreateBookingRequest(vehicleId, overlapStart, overlapEnd, "District 1, HCMC", false, null)),
            customer2Client.PostAsJsonAsync(
                "/api/bookings",
                new CreateBookingRequest(vehicleId, overlapStart, overlapEnd, "District 3, HCMC", false, null))
        };

        var overlapResponses = await Task.WhenAll(overlapTasks);
        overlapResponses.Select(r => r.StatusCode)
            .Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);

        // 2. Concurrent non-overlapping requests for the same vehicle -> both succeed (201)
        var slotAStart = DateTimeOffset.UtcNow.AddDays(20);
        var slotAEnd = slotAStart.AddDays(2);
        var slotBStart = DateTimeOffset.UtcNow.AddDays(25);
        var slotBEnd = slotBStart.AddDays(2);

        var nonOverlapTasks = new[]
        {
            customer1Client.PostAsJsonAsync(
                "/api/bookings",
                new CreateBookingRequest(vehicleId, slotAStart, slotAEnd, "District 1, HCMC", false, null)),
            customer2Client.PostAsJsonAsync(
                "/api/bookings",
                new CreateBookingRequest(vehicleId, slotBStart, slotBEnd, "District 3, HCMC", false, null))
        };

        var nonOverlapResponses = await Task.WhenAll(nonOverlapTasks);
        nonOverlapResponses.Select(r => r.StatusCode)
            .Should().AllBeEquivalentTo(HttpStatusCode.Created);
    }
}
