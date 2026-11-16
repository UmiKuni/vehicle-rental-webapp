using System.Reflection;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Availability.Entities;
using CarRental.Modules.Availability.Services;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Availability;

public class AvailabilityApiServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AvailabilityErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(VehicleCalendar).Assembly.GetType("CarRental.Modules.Availability.Errors.AvailabilityErrors");
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
        allErrors.Should().NotBeEmpty();

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex("^availability\\.[a-z_]+$", $"Error '{name}' must use availability.* snake_case naming.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();
        allErrors.Select(e => e.Name).Should().NotContain(n => n.Contains("SlotUnavailable3493D3") || n.Contains("SlotUnavailableEA8C70"));

        var slotUnavailable = allErrors.Single(e => e.Name == "SlotUnavailable").Error;
        slotUnavailable.Code.Should().Be("availability.slot_unavailable");
        slotUnavailable.Type.Should().Be(ErrorType.Conflict);

        var notCalendarOwner = allErrors.Single(e => e.Name == "NotCalendarOwner").Error;
        notCalendarOwner.Code.Should().Be("availability.not_calendar_owner");
        notCalendarOwner.Type.Should().Be(ErrorType.Forbidden);

        var invalidVehicle = allErrors.Single(e => e.Name == "InvalidVehicle").Error;
        invalidVehicle.Code.Should().Be("availability.invalid_vehicle");
        invalidVehicle.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_ShouldReflectActiveEntriesAndIgnoreExpiredHolds()
    {
        var harness = CreateHarness(Guid.NewGuid());
        var vehicleId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        var emptyCheck = await harness.Service.CheckAvailabilityAsync(vehicleId, range);
        emptyCheck.IsSuccess.Should().BeTrue();
        emptyCheck.Value.Should().BeTrue();

        var holdResult = await harness.Service.PlaceHoldAsync(vehicleId, bookingId, range, TimeSpan.FromHours(1));
        holdResult.IsSuccess.Should().BeTrue();

        var busyCheck = await harness.Service.CheckAvailabilityAsync(vehicleId, range);
        busyCheck.IsSuccess.Should().BeTrue();
        busyCheck.Value.Should().BeFalse();

        // Advance past 1h hold expiry
        harness.AdvanceTime(TimeSpan.FromHours(2));
        var expiredCheck = await harness.Service.CheckAvailabilityAsync(vehicleId, range);
        expiredCheck.IsSuccess.Should().BeTrue();
        expiredCheck.Value.Should().BeTrue();
    }

    [Fact]
    public async Task CheckVehicleAvailabilityAsync_ShouldReturnBusyPeriodsWhenOverlapping()
    {
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var harness = CreateHarness(ownerId);

        var range1 = DateTimeRange.Create(Now.AddDays(1), Now.AddDays(3)).Value;
        var range2 = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        var availableResult = await harness.Service.CheckVehicleAvailabilityAsync(vehicleId, range2);
        availableResult.IsSuccess.Should().BeTrue();
        availableResult.Value.IsAvailable.Should().BeTrue();
        availableResult.Value.BusyPeriods.Should().BeEmpty();
        availableResult.Value.ConflictReason.Should().BeNull();

        await harness.Service.BlockAsync(vehicleId, ownerId, range1, "Maintenance", CalendarEntryType.Maintenance);

        var unavailableResult = await harness.Service.CheckVehicleAvailabilityAsync(vehicleId, range2);
        unavailableResult.IsSuccess.Should().BeTrue();
        unavailableResult.Value.IsAvailable.Should().BeFalse();
        unavailableResult.Value.ConflictReason.Should().NotBeNullOrWhiteSpace();
        unavailableResult.Value.BusyPeriods.Should().ContainSingle(p => p.Start == range1.Start && p.End == range1.End);
    }

    [Fact]
    public async Task GetCalendarAsync_ShouldFilterExpiredAndOutOfRangeAndOrderAscending()
    {
        var ownerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var harness = CreateHarness(ownerId);

        // Place short hold (1h TTL) that will expire, plus two blocks in different order
        await harness.Service.PlaceHoldAsync(vehicleId, Guid.NewGuid(), DateTimeRange.Create(Now.AddDays(2), Now.AddDays(3)).Value, TimeSpan.FromHours(1));
        await harness.Service.BlockAsync(vehicleId, ownerId, DateTimeRange.Create(Now.AddDays(10), Now.AddDays(12)).Value, "Later");
        await harness.Service.BlockAsync(vehicleId, ownerId, DateTimeRange.Create(Now.AddDays(4), Now.AddDays(6)).Value, "Earlier");

        // Advance time so the 1h hold expires (blocks are still in the future at Now+2h)
        harness.AdvanceTime(TimeSpan.FromHours(2));

        var invalidRange = await harness.Service.GetCalendarAsync(vehicleId, Now.AddDays(5), Now.AddDays(4));
        invalidRange.IsFailure.Should().BeTrue();
        invalidRange.Error.Code.Should().Be("availability.invalid_range");

        var filtered = await harness.Service.GetCalendarAsync(vehicleId, Now.AddDays(1), Now.AddDays(8));
        filtered.IsSuccess.Should().BeTrue();
        filtered.Value.Should().HaveCount(1);
        filtered.Value[0].StartTime.Should().Be(Now.AddDays(4));

        var allActive = await harness.Service.GetCalendarAsync(vehicleId);
        allActive.IsSuccess.Should().BeTrue();
        allActive.Value.Should().HaveCount(2);
        allActive.Value[0].StartTime.Should().Be(Now.AddDays(4));
        allActive.Value[1].StartTime.Should().Be(Now.AddDays(10));
    }

    [Fact]
    public async Task HoldLifecycle_PlaceConfirmAndRelease_ShouldWorkAndHandleErrors()
    {
        var harness = CreateHarness(Guid.NewGuid());
        var vehicleId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        // Confirm on missing calendar fails with HoldNotFoundOrExpired
        var confirmMissing = await harness.Service.ConfirmHoldAsync(vehicleId, bookingId);
        confirmMissing.IsFailure.Should().BeTrue();
        confirmMissing.Error.Code.Should().Be("availability.hold_not_found_or_expired");
        confirmMissing.Error.Type.Should().Be(ErrorType.NotFound);

        // Release on missing calendar is idempotent success
        var releaseMissing = await harness.Service.ReleaseHoldAsync(vehicleId, bookingId);
        releaseMissing.IsSuccess.Should().BeTrue();

        // Place hold creates calendar and saves
        var placeResult = await harness.Service.PlaceHoldAsync(vehicleId, bookingId, range, TimeSpan.FromHours(2));
        placeResult.IsSuccess.Should().BeTrue();
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Overlapping hold returns Conflict
        var overlappingPlace = await harness.Service.PlaceHoldAsync(vehicleId, Guid.NewGuid(), range, TimeSpan.FromHours(2));
        overlappingPlace.IsFailure.Should().BeTrue();
        overlappingPlace.Error.Code.Should().Be("availability.slot_unavailable");
        overlappingPlace.Error.Type.Should().Be(ErrorType.Conflict);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Confirm transitions hold to Booked
        var confirmResult = await harness.Service.ConfirmHoldAsync(vehicleId, bookingId);
        confirmResult.IsSuccess.Should().BeTrue();
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        harness.Calendars[vehicleId].Entries.Single().Type.Should().Be(CalendarEntryType.Booked);

        // Release removes entry
        var releaseResult = await harness.Service.ReleaseHoldAsync(vehicleId, bookingId);
        releaseResult.IsSuccess.Should().BeTrue();
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        harness.Calendars[vehicleId].Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task BlockAndUnblockAsync_ShouldEnforceOwnershipAndDetectConflicts()
    {
        var ownerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var harness = CreateHarness(ownerId);
        var range = DateTimeRange.Create(Now.AddDays(3), Now.AddDays(5)).Value;

        // Non-owner block fails with Forbidden
        var nonOwnerBlock = await harness.Service.BlockAsync(vehicleId, intruderId, range, "Unauthorized");
        nonOwnerBlock.IsFailure.Should().BeTrue();
        nonOwnerBlock.Error.Code.Should().Be("availability.not_calendar_owner");
        nonOwnerBlock.Error.Type.Should().Be(ErrorType.Forbidden);

        // Owner block succeeds
        var ownerBlock = await harness.Service.BlockAsync(vehicleId, ownerId, range, "Road trip", CalendarEntryType.OwnerBlocked);
        ownerBlock.IsSuccess.Should().BeTrue();
        ownerBlock.Value.Type.Should().Be(nameof(CalendarEntryType.OwnerBlocked));
        ownerBlock.Value.Note.Should().Be("Road trip");
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Overlapping block fails with Conflict
        var conflictBlock = await harness.Service.BlockAsync(vehicleId, ownerId, range, "Duplicate");
        conflictBlock.IsFailure.Should().BeTrue();
        conflictBlock.Error.Code.Should().Be("availability.slot_unavailable");
        conflictBlock.Error.Type.Should().Be(ErrorType.Conflict);

        // Unblock non-existent entry fails with NotFound
        var missingUnblock = await harness.Service.UnblockAsync(Guid.NewGuid(), ownerId);
        missingUnblock.IsFailure.Should().BeTrue();
        missingUnblock.Error.Code.Should().Be("availability.entry_not_found");
        missingUnblock.Error.Type.Should().Be(ErrorType.NotFound);

        // Unblock by non-owner fails with Forbidden
        var intruderUnblock = await harness.Service.UnblockAsync(ownerBlock.Value.Id, intruderId);
        intruderUnblock.IsFailure.Should().BeTrue();
        intruderUnblock.Error.Code.Should().Be("availability.not_calendar_owner");
        intruderUnblock.Error.Type.Should().Be(ErrorType.Forbidden);

        // Unblock by owner removes block
        var ownerUnblock = await harness.Service.UnblockAsync(ownerBlock.Value.Id, ownerId);
        ownerUnblock.IsSuccess.Should().BeTrue();
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        harness.Calendars[vehicleId].Entries.Should().BeEmpty();
    }

    private static (
        AvailabilityApiService Service,
        Mock<AvailabilityDbContext> DbContextMock,
        Dictionary<Guid, VehicleCalendar> Calendars,
        Action<TimeSpan> AdvanceTime) CreateHarness(Guid vehicleOwnerId)
    {
        var currentTime = Now;
        var calendars = new Dictionary<Guid, VehicleCalendar>();

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(() => currentTime);

        var dbContextMock = new Mock<AvailabilityDbContext>(new DbContextOptionsBuilder<AvailabilityDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var fleetMock = new Mock<IFleetApi>();
        fleetMock.Setup(x => x.GetVehicleSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, CancellationToken _) => Result<VehicleSummaryDto>.Success(
                new VehicleSummaryDto(
                    vehicleId,
                    vehicleOwnerId,
                    "Published",
                    Guid.NewGuid(),
                    "Toyota",
                    Guid.NewGuid(),
                    "Camry",
                    "51A-12345",
                    "Automatic",
                    5,
                    null,
                    GeoPoint.Create(10.7769, 106.7009).Value,
                    "Ho Chi Minh City")));

        var repoMock = new Mock<IVehicleCalendarRepository>();
        repoMock.Setup(x => x.GetByVehicleIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, CancellationToken _) => calendars.GetValueOrDefault(vehicleId));
        repoMock.Setup(x => x.GetByEntryIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid entryId, CancellationToken _) => calendars.Values.SingleOrDefault(c => c.Entries.Any(e => e.Id == entryId)));
        repoMock.Setup(x => x.HasActiveOverlapAsync(It.IsAny<Guid>(), It.IsAny<DateTimeRange>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, DateTimeRange range, DateTimeOffset now, CancellationToken _) =>
                calendars.TryGetValue(vehicleId, out var calendar)
                && calendar.Entries.Any(e => e.IsActive(now) && e.TimeRange.Overlaps(range)));
        repoMock.Setup(x => x.GetActiveOverlappingEntriesAsync(It.IsAny<Guid>(), It.IsAny<DateTimeRange>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, DateTimeRange range, DateTimeOffset now, CancellationToken _) =>
            {
                if (!calendars.TryGetValue(vehicleId, out var calendar))
                    return Array.Empty<CalendarEntry>();

                return calendar.Entries
                    .Where(e => e.IsActive(now) && e.TimeRange.Overlaps(range))
                    .OrderBy(e => e.TimeRange.Start)
                    .ToList();
            });
        repoMock.Setup(x => x.GetActiveEntriesAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now, CancellationToken _) =>
            {
                if (!calendars.TryGetValue(vehicleId, out var calendar))
                    return Array.Empty<CalendarEntry>();

                IEnumerable<CalendarEntry> query = calendar.Entries.Where(e => e.IsActive(now));

                if (from.HasValue)
                    query = query.Where(e => e.TimeRange.End > from.Value);

                if (to.HasValue)
                    query = query.Where(e => e.TimeRange.Start < to.Value);

                return query.OrderBy(e => e.TimeRange.Start).ToList();
            });
        repoMock.Setup(x => x.Add(It.IsAny<VehicleCalendar>()))
            .Callback((VehicleCalendar calendar) => calendars[calendar.VehicleId] = calendar);

        var service = new AvailabilityApiService(
            dbContextMock.Object,
            repoMock.Object,
            fleetMock.Object,
            clockMock.Object);

        return (service, dbContextMock, calendars, delta => currentTime = currentTime.Add(delta));
    }
}
