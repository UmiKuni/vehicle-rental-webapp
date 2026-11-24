using System.Net;
using System.Net.Http.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Availability.Contracts.Dtos;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Availability;

public class AvailabilityIntegrationTests : IntegrationTestBase
{
    public AvailabilityIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    private async Task<Guid> SeedVehicleAsync(Guid ownerId, string plateNumber)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

        var brand = new VehicleBrand(Guid.NewGuid(), "Honda");
        var model = new VehicleModel(Guid.NewGuid(), brand.Id, "Civic");
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

    [Fact]
    public async Task Block_Check_GetCalendar_And_Unblock_AsOwner_ShouldPersistInDatabase()
    {
        var (ownerClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail@example.com",
            "0904445566",
            taxCode: "0102030409");
        var vehicleId = await SeedVehicleAsync(ownerId, "51F-666.01");

        var start = DateTimeOffset.UtcNow.AddDays(5);
        var end = DateTimeOffset.UtcNow.AddDays(7);

        // 1. Block schedule
        var blockResponse = await ownerClient.PostAsJsonAsync(
            $"/api/availability/vehicles/{vehicleId}/blocks",
            new BlockScheduleRequest(start, end, "Personal trip", CalendarEntryType.OwnerBlocked));

        blockResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdEntry = await blockResponse.Content.ReadFromJsonAsync<CalendarEntryDto>();
        createdEntry.Should().NotBeNull();
        createdEntry!.VehicleId.Should().Be(vehicleId);
        createdEntry.Type.Should().Be(nameof(CalendarEntryType.OwnerBlocked));
        createdEntry.Note.Should().Be("Personal trip");

        // 2. Overlapping block returns 409 Conflict
        var overlapResponse = await ownerClient.PostAsJsonAsync(
            $"/api/availability/vehicles/{vehicleId}/blocks",
            new BlockScheduleRequest(start.AddHours(12), end.AddHours(12), "Overlap"));

        overlapResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 3. Check availability reports unavailable with busy period
        var checkUrl = $"/api/availability/vehicles/{vehicleId}/check?start={Uri.EscapeDataString(start.ToString("O"))}&end={Uri.EscapeDataString(end.ToString("O"))}";
        var checkResponse = await Client.GetAsync(checkUrl);
        checkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkResult = await checkResponse.Content.ReadFromJsonAsync<AvailabilityCheckResultDto>();
        checkResult.Should().NotBeNull();
        checkResult!.IsAvailable.Should().BeFalse();
        checkResult.BusyPeriods.Should().HaveCount(1);

        // 4. Calendar query returns the OwnerBlocked entry with persisted Note
        var calendarUrl = $"/api/availability/vehicles/{vehicleId}/calendar?from={Uri.EscapeDataString(start.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(end.AddDays(1).ToString("O"))}";
        var calendarResponse = await Client.GetAsync(calendarUrl);
        calendarResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var entries = await calendarResponse.Content.ReadFromJsonAsync<IReadOnlyList<CalendarEntryDto>>();
        entries.Should().ContainSingle(e =>
            e.Id == createdEntry.Id
            && e.Type == nameof(CalendarEntryType.OwnerBlocked)
            && e.Note == "Personal trip");

        // 5. Unblock deletes the entry
        var unblockResponse = await ownerClient.DeleteAsync($"/api/availability/blocks/{createdEntry.Id}");
        unblockResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // 6. Availability check is now available again
        var recheckResponse = await Client.GetAsync(checkUrl);
        recheckResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var recheckResult = await recheckResponse.Content.ReadFromJsonAsync<AvailabilityCheckResultDto>();
        recheckResult!.IsAvailable.Should().BeTrue();
        recheckResult.BusyPeriods.Should().BeEmpty();
    }

    [Fact]
    public async Task Block_AsNonOwner_ShouldReturn403Forbidden()
    {
        var (_, ownerAId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail_a@example.com",
            "0905556677",
            taxCode: "0102030410");
        var (ownerBClient, _) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail_b@example.com",
            "0906667788",
            taxCode: "0102030411");
        var vehicleId = await SeedVehicleAsync(ownerAId, "51F-666.02");

        var start = DateTimeOffset.UtcNow.AddDays(3);
        var end = DateTimeOffset.UtcNow.AddDays(4);

        var response = await ownerBClient.PostAsJsonAsync(
            $"/api/availability/vehicles/{vehicleId}/blocks",
            new BlockScheduleRequest(start, end, "Intruder block", CalendarEntryType.OwnerBlocked));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PlaceHold_ConfirmHold_And_ReleaseHold_ViaAvailabilityApi_ShouldUpdateCalendar()
    {
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail_hold@example.com",
            "0907778899",
            taxCode: "0102030412");
        var vehicleId = await SeedVehicleAsync(ownerId, "51F-666.03");

        var start = DateTimeOffset.UtcNow.AddDays(4);
        var end = DateTimeOffset.UtcNow.AddDays(6);
        var range = DateTimeRange.Create(start, end).Value;
        var bookingId = Guid.NewGuid();

        using (var scope = CreateScope())
        {
            var api = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
            var placeResult = await api.PlaceHoldAsync(vehicleId, bookingId, range, TimeSpan.FromHours(2));
            placeResult.IsSuccess.Should().BeTrue();

            var overlappingResult = await api.PlaceHoldAsync(vehicleId, Guid.NewGuid(), range, TimeSpan.FromHours(2));
            overlappingResult.IsFailure.Should().BeTrue();
            overlappingResult.Error.Code.Should().Be("availability.slot_unavailable");

            var confirmResult = await api.ConfirmHoldAsync(vehicleId, bookingId);
            confirmResult.IsSuccess.Should().BeTrue();
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var entry = await db.CalendarEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.BookingId == bookingId);
            entry.Should().NotBeNull();
            entry!.Type.Should().Be(CalendarEntryType.Booked);
            entry.ExpiresAt.Should().BeNull();

            var api = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
            var releaseResult = await api.ReleaseHoldAsync(vehicleId, bookingId);
            releaseResult.IsSuccess.Should().BeTrue();
        }

        using (var scope = CreateScope())
        {
            var api = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
            var available = await api.CheckAvailabilityAsync(vehicleId, range);
            available.IsSuccess.Should().BeTrue();
            available.Value.Should().BeTrue();
        }
    }

    [Fact]
    public async Task EventDriven_BookingCancelled_ShouldReleaseHoldInCalendar()
    {
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail_event@example.com",
            "0908889900",
            taxCode: "0102030413");
        var vehicleId = await SeedVehicleAsync(ownerId, "51F-666.04");
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(DateTimeOffset.UtcNow.AddDays(8), DateTimeOffset.UtcNow.AddDays(10)).Value;

        using (var scope = CreateScope())
        {
            var api = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
            (await api.PlaceHoldAsync(vehicleId, bookingId, range, TimeSpan.FromHours(2))).IsSuccess.Should().BeTrue();

            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new BookingCancelled(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                bookingId,
                ownerId,
                new Money(0, "VND"),
                new Money(500_000, "VND"),
                "Customer cancelled"));
        }

        var released = false;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var exists = await db.CalendarEntries
                .AsNoTracking()
                .AnyAsync(e => e.BookingId == bookingId);
            if (!exists)
            {
                released = true;
                break;
            }

            await Task.Delay(150);
        }

        released.Should().BeTrue();
    }

    [Fact]
    public async Task PlaceHold_WhenExpiredHoldOverlapsInDatabase_ShouldPurgeAndSucceedWithoutExclusionViolation()
    {
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_avail_expired@example.com",
            "0908889911",
            taxCode: "0102030414");
        var vehicleId = await SeedVehicleAsync(ownerId, "51F-666.05");

        var start = DateTimeOffset.UtcNow.AddDays(12);
        var end = DateTimeOffset.UtcNow.AddDays(14);
        var range = DateTimeRange.Create(start, end).Value;
        var expiredBookingId = Guid.NewGuid();
        var freshBookingId = Guid.NewGuid();

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var calendar = VehicleCalendar.Create(vehicleId, DateTimeOffset.UtcNow.AddHours(-2));
            db.VehicleCalendars.Add(calendar);
            db.CalendarEntries.Add(new CalendarEntry(
                Guid.CreateVersion7(),
                vehicleId,
                CalendarEntryType.Hold,
                range,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                expiredBookingId));
            await db.SaveChangesAsync();
        }

        using (var scope = CreateScope())
        {
            var api = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
            var placeResult = await api.PlaceHoldAsync(vehicleId, freshBookingId, range, TimeSpan.FromHours(2));
            placeResult.IsSuccess.Should().BeTrue();
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();
            var entries = await db.CalendarEntries
                .AsNoTracking()
                .Where(e => e.VehicleId == vehicleId)
                .ToListAsync();

            entries.Should().ContainSingle(e => e.BookingId == freshBookingId);
        }
    }
}
