using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class AvailabilityModuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlaceHold_OverlappingWithExistingHold_ShouldFail()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var range1 = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;
        var booking1 = Guid.NewGuid();

        var hold1Result = calendar.PlaceHold(booking1, range1, TimeSpan.FromHours(2), Now);
        hold1Result.IsSuccess.Should().BeTrue();

        // Second booking requests overlapping dates (e.g. Day 3 to 5)
        var range2 = DateTimeRange.Create(Now.AddDays(3), Now.AddDays(5)).Value;
        var booking2 = Guid.NewGuid();

        var hold2Result = calendar.PlaceHold(booking2, range2, TimeSpan.FromHours(2), Now);

        hold2Result.IsSuccess.Should().BeFalse();
        hold2Result.Error.Code.Should().Be("availability.slot_unavailable");
    }

    [Fact]
    public void PlaceHold_WithOver12HoursTtl_ShouldBeClampedTo12Hours()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;
        var bookingId = Guid.NewGuid();

        // Request 24h TTL
        var holdResult = calendar.PlaceHold(bookingId, range, TimeSpan.FromHours(24), Now);

        holdResult.IsSuccess.Should().BeTrue();
        var entry = calendar.Entries.Single(e => e.BookingId == bookingId);

        // Clamped to 12 hours from Now
        entry.ExpiresAt.Should().Be(Now.AddHours(12));
    }

    [Fact]
    public void ConfirmHold_ActiveHold_ShouldBecomeBookedAndClearExpiresAt()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        calendar.PlaceHold(bookingId, range, TimeSpan.FromHours(2), Now);

        var confirmResult = calendar.ConfirmHold(bookingId, Now.AddHours(1));
        confirmResult.IsSuccess.Should().BeTrue();

        var entry = calendar.Entries.Single(e => e.BookingId == bookingId);
        entry.Type.Should().Be(CalendarEntryType.Booked);
        entry.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void ConfirmHold_WhenExpired_ShouldFail()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        calendar.PlaceHold(bookingId, range, TimeSpan.FromHours(2), Now);

        // Advance time past expiration (Now + 3h):
        var futureTime = Now.AddHours(3);
        var confirmResult = calendar.ConfirmHold(bookingId, futureTime);

        confirmResult.IsSuccess.Should().BeFalse();
        confirmResult.Error.Code.Should().Be("availability.hold_not_found_or_expired");
    }

    [Fact]
    public void Release_ShouldRemoveBookingEntries()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var bookingId = Guid.NewGuid();
        var range = DateTimeRange.Create(Now.AddDays(2), Now.AddDays(4)).Value;

        calendar.PlaceHold(bookingId, range, TimeSpan.FromHours(2), Now);
        calendar.Entries.Should().ContainSingle(e => e.BookingId == bookingId);

        var releaseResult = calendar.Release(bookingId, Now);

        releaseResult.IsSuccess.Should().BeTrue();
        calendar.Entries.Should().BeEmpty();

        // Vehicle is now available again for that period!
        calendar.IsAvailable(range, Now).Should().BeTrue();
    }

    [Fact]
    public void Block_And_Unblock_OwnerOrMaintenance_ShouldWorkCorrectly()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var range = DateTimeRange.Create(Now.AddDays(5), Now.AddDays(7)).Value;

        var blockResult = calendar.Block(CalendarEntryType.Maintenance, range, Now);
        blockResult.IsSuccess.Should().BeTrue();

        calendar.IsAvailable(range, Now).Should().BeFalse();

        var entry = calendar.Entries.Single(e => e.Type == CalendarEntryType.Maintenance);
        var unblockResult = calendar.Unblock(entry.Id, Now);

        unblockResult.IsSuccess.Should().BeTrue();
        calendar.IsAvailable(range, Now).Should().BeTrue();
    }

    [Fact]
    public void ExpireHolds_ShouldRemoveOnlyExpiredHolds()
    {
        var calendar = VehicleCalendar.Create(Guid.NewGuid(), Now);
        var booking1 = Guid.NewGuid();
        var booking2 = Guid.NewGuid();

        calendar.PlaceHold(booking1, DateTimeRange.Create(Now.AddDays(1), Now.AddDays(2)).Value, TimeSpan.FromHours(1), Now);
        calendar.PlaceHold(booking2, DateTimeRange.Create(Now.AddDays(3), Now.AddDays(4)).Value, TimeSpan.FromHours(5), Now);

        // At Now + 2h: booking1 hold has expired, booking2 hold is still active
        var expiredCount = calendar.ExpireHolds(Now.AddHours(2));
        expiredCount.Should().Be(1);

        calendar.Entries.Should().ContainSingle(e => e.BookingId == booking2);
    }
}
