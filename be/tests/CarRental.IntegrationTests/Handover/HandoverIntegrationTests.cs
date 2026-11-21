using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Handover.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Handover.Entities;
using CarRental.Modules.Pricing.Contracts.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CarRental.IntegrationTests.Handover;

public class HandoverIntegrationTests : IntegrationTestBase
{
    public HandoverIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVehicleAsync(Guid ownerId, string plateNumber)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var brand = new VehicleBrand(Guid.NewGuid(), "Toyota");
        var model = new VehicleModel(Guid.NewGuid(), brand.Id, "Vios");
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

    private static HttpClient CreateClientWithTokenFrom(HttpClient sourceClient, Func<HttpClient> factory)
    {
        var client = factory();
        client.DefaultRequestHeaders.Authorization = sourceClient.DefaultRequestHeaders.Authorization;
        return client;
    }

    [Fact]
    public async Task PickupAndReturnFlow_EndToEnd_ShouldPersistCalculateOverKmFeeAndEnforceImmutability()
    {
        var (ownerAuthClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_handover@example.com",
            "0901113344",
            taxCode: "0102030420");
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_handover@example.com",
            "0902224455");

        var vehicleId = await SeedVehicleAsync(ownerId, "51H-777.01");

        // Configure pricing with 200 km/day limit and 5,000 VND/km over-km fee
        var pricingResponse = await ownerAuthClient.PutAsJsonAsync(
            $"/api/pricing/vehicles/{vehicleId}",
            new ConfigurePricingDto(
                HourlyRate: null,
                WeekdayRate: new Money(600_000, "VND"),
                WeekendRate: new Money(800_000, "VND"),
                DepositAmount: new Money(5_000_000, "VND"),
                KmLimit: 200,
                OverKmFee: new Money(5_000, "VND"),
                CleaningFee: null,
                OwnerId: Guid.Empty));
        pricingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var bookingId = Guid.NewGuid();
        var startTime = new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);
        var endTime = startTime.AddDays(2); // 2 days => 400 km allowed

        var bookingState = new FakeBookingState();
        bookingState.Bookings[bookingId] = new FakeBookingRecord(
            bookingId,
            customerId,
            ownerId,
            vehicleId,
            "Paid",
            startTime,
            endTime,
            1_200_000m,
            5_000_000m);

        using var customFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(bookingState);
                services.RemoveAll<IBookingApi>();
                services.AddScoped<IBookingApi, FakeBookingApi>();
            }));

        using var ownerClient = CreateClientWithTokenFrom(ownerAuthClient, () => customFactory.CreateClient());
        using var customerClient = CreateClientWithTokenFrom(customerAuthClient, () => customFactory.CreateClient());

        // 1. Create Pickup handover
        var createPickupResp = await ownerClient.PostAsJsonAsync(
            "/api/handovers/",
            new CreateHandoverRequest(bookingId, "Pickup"));
        createPickupResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var pickup = await createPickupResp.Content.ReadFromJsonAsync<HandoverDetailDto>();
        pickup.Should().NotBeNull();
        pickup!.Type.Should().Be("Pickup");
        pickup.Status.Should().Be("Draft");

        // 2. Update condition twice (tests in-place VehicleCondition update on PostgreSQL)
        var cond1Resp = await ownerClient.PutAsJsonAsync(
            $"/api/handovers/{pickup.Id}/condition",
            new UpdateVehicleConditionRequest(10_000, 100, "Clean"));
        cond1Resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var cond2Resp = await customerClient.PutAsJsonAsync(
            $"/api/handovers/{pickup.Id}/condition",
            new UpdateVehicleConditionRequest(10_000, 95, "Minor scuff on front bumper"));
        cond2Resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedCond = await cond2Resp.Content.ReadFromJsonAsync<VehicleConditionDto>();
        updatedCond!.FuelLevel.Should().Be(95);
        updatedCond.DamageNote.Should().Be("Minor scuff on front bumper");

        // 3. Upload all 5 mandatory photos + replace FrontLeft once
        var capturedAt = DateTimeOffset.UtcNow;
        var replaceResp = await ownerClient.PostAsJsonAsync(
            $"/api/handovers/{pickup.Id}/photos",
            new UploadHandoverPhotoRequest("FrontLeft", "seed/fl_old.jpg", "HASH_OLD", capturedAt));
        replaceResp.StatusCode.Should().Be(HttpStatusCode.Created);

        foreach (var corner in Enum.GetValues<PhotoCorner>())
        {
            var photoResp = await ownerClient.PostAsJsonAsync(
                $"/api/handovers/{pickup.Id}/photos",
                new UploadHandoverPhotoRequest(corner.ToString(), $"seed/{corner}.jpg", $"HASH_{corner}", capturedAt));
            photoResp.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // 4. Get by booking & by id
        var listResp = await customerClient.GetAsync($"/api/handovers/booking/{bookingId}");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var summaries = await listResp.Content.ReadFromJsonAsync<IReadOnlyList<HandoverSummaryDto>>();
        summaries.Should().ContainSingle(s => s.Id == pickup.Id && s.Type == "Pickup" && s.Status == "Draft");

        // 5. Dual confirm Pickup
        var custConfirmResp = await customerClient.PutAsJsonAsync(
            $"/api/handovers/{pickup.Id}/confirm",
            new ConfirmHandoverRequest("Customer", "sig-cust"));
        custConfirmResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterCust = await custConfirmResp.Content.ReadFromJsonAsync<HandoverDetailDto>();
        afterCust!.Status.Should().Be("Draft");
        afterCust.IsConfirmedByCustomer.Should().BeTrue();
        afterCust.IsConfirmedByOwner.Should().BeFalse();

        var ownerConfirmResp = await ownerClient.PutAsJsonAsync(
            $"/api/handovers/{pickup.Id}/confirm",
            new ConfirmHandoverRequest("Owner", "sig-owner"));
        ownerConfirmResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalizedPickup = await ownerConfirmResp.Content.ReadFromJsonAsync<HandoverDetailDto>();
        finalizedPickup!.Status.Should().Be("Confirmed");
        finalizedPickup.IsConfirmedByOwner.Should().BeTrue();
        finalizedPickup.FinalizedAt.Should().NotBeNull();
        finalizedPickup.Photos.Should().HaveCount(5);

        bookingState.Bookings[bookingId].Status.Should().Be("InProgress");

        // 6. Immutability after Confirmed -> 409 Conflict
        var postConfirmPhotoResp = await ownerClient.PostAsJsonAsync(
            $"/api/handovers/{pickup.Id}/photos",
            new UploadHandoverPhotoRequest("Dash", "seed/dash_new.jpg", "HASH_NEW", capturedAt));
        postConfirmPhotoResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 7. Create & complete Return handover with 550 km driven (150 km over * 5,000 = 750,000 VND)
        var createReturnResp = await customerClient.PostAsJsonAsync(
            "/api/handovers/",
            new CreateHandoverRequest(bookingId, "Return"));
        createReturnResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var ret = await createReturnResp.Content.ReadFromJsonAsync<HandoverDetailDto>();

        var retCondResp = await customerClient.PutAsJsonAsync(
            $"/api/handovers/{ret!.Id}/condition",
            new UpdateVehicleConditionRequest(10_550, 80, "Returned clean"));
        retCondResp.StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var corner in Enum.GetValues<PhotoCorner>())
        {
            var photoResp = await customerClient.PostAsJsonAsync(
                $"/api/handovers/{ret.Id}/photos",
                new UploadHandoverPhotoRequest(corner.ToString(), $"seed/ret_{corner}.jpg", $"HASH_RET_{corner}", capturedAt));
            photoResp.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        (await ownerClient.PutAsJsonAsync($"/api/handovers/{ret.Id}/confirm", new ConfirmHandoverRequest("Owner", null)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var retFinalResp = await customerClient.PutAsJsonAsync(
            $"/api/handovers/{ret.Id}/confirm",
            new ConfirmHandoverRequest("Customer", null));
        retFinalResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalizedReturn = await retFinalResp.Content.ReadFromJsonAsync<HandoverDetailDto>();
        finalizedReturn!.Status.Should().Be("Confirmed");

        bookingState.Bookings[bookingId].Status.Should().Be("Completed");
        bookingState.Bookings[bookingId].OverKmFee.Should().Be(750_000m);

        // 8. Verify persistence in HandoverDbContext
        using var scope = CreateScope();
        var handoverDb = scope.ServiceProvider.GetRequiredService<HandoverDbContext>();
        var dbHandovers = await handoverDb.Handovers
            .AsNoTracking()
            .Where(h => h.BookingId == bookingId)
            .ToListAsync();
        dbHandovers.Should().HaveCount(2);
        dbHandovers.Should().OnlyContain(h => h.Status == HandoverStatus.Confirmed);
    }

    [Fact]
    public async Task HandoverEndpoints_UnauthorizedAndForbiddenChecks_ShouldReturnExpectedStatusCodes()
    {
        var (ownerAuthClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_auth_ho@example.com",
            "0903335566",
            taxCode: "0102030421");
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_auth_ho@example.com",
            "0904446677");
        var (outsiderAuthClient, _) = await CreateAuthenticatedCustomerClientAsync(
            "outsider_ho@example.com",
            "0905557788");

        var bookingId = Guid.NewGuid();
        var bookingState = new FakeBookingState();
        bookingState.Bookings[bookingId] = new FakeBookingRecord(
            bookingId,
            customerId,
            ownerId,
            Guid.NewGuid(),
            "Paid",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1),
            500_000m,
            2_000_000m);

        using var customFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(bookingState);
                services.RemoveAll<IBookingApi>();
                services.AddScoped<IBookingApi, FakeBookingApi>();
            }));

        using var anonClient = customFactory.CreateClient();
        using var ownerClient = CreateClientWithTokenFrom(ownerAuthClient, () => customFactory.CreateClient());
        using var customerClient = CreateClientWithTokenFrom(customerAuthClient, () => customFactory.CreateClient());
        using var outsiderClient = CreateClientWithTokenFrom(outsiderAuthClient, () => customFactory.CreateClient());

        // Unauthenticated -> 401
        var unauthResp = await anonClient.PostAsJsonAsync("/api/handovers/", new CreateHandoverRequest(bookingId, "Pickup"));
        unauthResp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Outsider create -> 403
        var outsiderCreate = await outsiderClient.PostAsJsonAsync("/api/handovers/", new CreateHandoverRequest(bookingId, "Pickup"));
        outsiderCreate.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Owner creates pickup
        var createResp = await ownerClient.PostAsJsonAsync("/api/handovers/", new CreateHandoverRequest(bookingId, "Pickup"));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var pickup = await createResp.Content.ReadFromJsonAsync<HandoverDetailDto>();

        // Outsider get by id -> 403
        var outsiderGet = await outsiderClient.GetAsync($"/api/handovers/{pickup!.Id}");
        outsiderGet.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Customer confirming as Owner -> 403
        var roleMismatch = await customerClient.PutAsJsonAsync(
            $"/api/handovers/{pickup.Id}/confirm",
            new ConfirmHandoverRequest("Owner", null));
        roleMismatch.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Unknown ID -> 404
        var notFoundResp = await ownerClient.GetAsync($"/api/handovers/{Guid.NewGuid()}");
        notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadHandoverPhoto_MultipartFile_ShouldStoreInImageStorageAndReturnSignedPrivateUrl()
    {
        var (ownerAuthClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_upload_ho@example.com",
            "0906668899",
            taxCode: "0102030422");
        var (_, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_upload_ho@example.com",
            "0907779900");

        var bookingId = Guid.NewGuid();
        var bookingState = new FakeBookingState();
        bookingState.Bookings[bookingId] = new FakeBookingRecord(
            bookingId,
            customerId,
            ownerId,
            Guid.NewGuid(),
            "Paid",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(1),
            500_000m,
            2_000_000m);

        using var customFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton(bookingState);
                services.RemoveAll<IBookingApi>();
                services.AddScoped<IBookingApi, FakeBookingApi>();
            }));

        using var ownerClient = CreateClientWithTokenFrom(ownerAuthClient, () => customFactory.CreateClient());

        var createResp = await ownerClient.PostAsJsonAsync(
            "/api/handovers/",
            new CreateHandoverRequest(bookingId, "Pickup"));
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var handover = (await createResp.Content.ReadFromJsonAsync<HandoverDetailDto>())!;

        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(jpegBytes));

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("FrontLeft"), "corner");
        var fileContent = new ByteArrayContent(jpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "front-left.jpg");

        var uploadResp = await ownerClient.PostAsync($"/api/handovers/{handover.Id}/photos", form);
        uploadResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var photo = await uploadResp.Content.ReadFromJsonAsync<HandoverPhotoDto>();
        photo.Should().NotBeNull();
        photo!.Corner.Should().Be("FrontLeft");
        photo.StorageKey.Should().StartWith($"handover/{bookingId:N}/");
        photo.FileHash.Should().Be(expectedHash);
        photo.ViewUrl.Should().StartWith($"https://storage.carrental.vn/private/{photo.StorageKey}?exp=")
            .And.Contain("&sig=");

        var detailResp = await ownerClient.GetAsync($"/api/handovers/{handover.Id}");
        detailResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await detailResp.Content.ReadFromJsonAsync<HandoverDetailDto>();
        detail!.Photos.Should().ContainSingle(p =>
            p.StorageKey == photo.StorageKey
            && p.FileHash == expectedHash
            && p.ViewUrl.Contains("exp="));
    }

    private sealed class FakeBookingRecord
    {
        public Guid BookingId { get; }
        public Guid CustomerId { get; }
        public Guid OwnerId { get; }
        public Guid VehicleId { get; }
        public string Status { get; set; }
        public DateTimeOffset StartTime { get; }
        public DateTimeOffset EndTime { get; }
        public decimal TotalRentAmount { get; }
        public decimal SecurityDepositAmount { get; }
        public decimal? OverKmFee { get; set; }

        public FakeBookingRecord(
            Guid bookingId,
            Guid customerId,
            Guid ownerId,
            Guid vehicleId,
            string status,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            decimal totalRentAmount,
            decimal securityDepositAmount)
        {
            BookingId = bookingId;
            CustomerId = customerId;
            OwnerId = ownerId;
            VehicleId = vehicleId;
            Status = status;
            StartTime = startTime;
            EndTime = endTime;
            TotalRentAmount = totalRentAmount;
            SecurityDepositAmount = securityDepositAmount;
        }
    }

    private sealed class FakeBookingState
    {
        public ConcurrentDictionary<Guid, FakeBookingRecord> Bookings { get; } = new();
    }

    private sealed class FakeBookingApi(FakeBookingState state) : IBookingApi
    {
        public Task<bool> IsParticipantAsync(Guid bookingId, Guid userId, CancellationToken ct = default)
        {
            var isParticipant = state.Bookings.TryGetValue(bookingId, out var b)
                                && (b.CustomerId == userId || b.OwnerId == userId);
            return Task.FromResult(isParticipant);
        }

        public Task<Result<BookingRentalDetailsDto>> GetRentalDetailsAsync(Guid bookingId, CancellationToken ct = default)
        {
            if (!state.Bookings.TryGetValue(bookingId, out var b))
                return Task.FromResult(Result.Failure<BookingRentalDetailsDto>(Error.NotFound("booking.not_found", "Booking not found.")));

            return Task.FromResult(Result.Success(new BookingRentalDetailsDto(
                b.BookingId,
                b.CustomerId,
                b.OwnerId,
                b.VehicleId,
                b.Status,
                b.StartTime,
                b.EndTime,
                b.TotalRentAmount,
                b.SecurityDepositAmount)));
        }

        public Task<Result> StartTripAsync(Guid bookingId, DateTimeOffset startedAt, CancellationToken ct = default)
        {
            if (!state.Bookings.TryGetValue(bookingId, out var b))
                return Task.FromResult(Result.Failure(Error.NotFound("booking.not_found", "Booking not found.")));

            b.Status = "InProgress";
            return Task.FromResult(Result.Success());
        }

        public Task<Result> CompleteTripAsync(Guid bookingId, decimal? overKmFee, DateTimeOffset completedAt, CancellationToken ct = default)
        {
            if (!state.Bookings.TryGetValue(bookingId, out var b))
                return Task.FromResult(Result.Failure(Error.NotFound("booking.not_found", "Booking not found.")));

            b.Status = "Completed";
            b.OverKmFee = overKmFee;
            return Task.FromResult(Result.Success());
        }

        public Task<Result<BookingDetailDto>> CreateAsync(Guid customerId, CreateBookingRequest request, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<PagedResult<BookingSummaryDto>>> GetMyBookingsAsync(Guid customerId, string? status = null, int page = 1, int pageSize = 10, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<PagedResult<BookingSummaryDto>>> GetOwnerBookingsAsync(Guid ownerId, string? status = null, int page = 1, int pageSize = 10, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingDetailDto>> GetByIdAsync(Guid bookingId, Guid actorId, bool isAdmin = false, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingDetailDto>> ApproveAsync(Guid bookingId, Guid ownerId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingDetailDto>> RejectAsync(Guid bookingId, Guid ownerId, string reason, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingCancellationResultDto>> CancelAsync(Guid bookingId, Guid customerId, string reason, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result<BookingDetailDto>> AdminCancelAsync(Guid bookingId, Guid adminId, string reason, string penaltyParty, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<Result> MarkPaidAsync(Guid bookingId, Guid paymentId, DateTimeOffset paidAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
