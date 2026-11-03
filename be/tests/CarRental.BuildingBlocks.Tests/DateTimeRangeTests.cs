using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Tests;

public class DateTimeRangeTests
{
    private readonly DateTimeOffset _baseTime = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidRange_ReturnsSuccess()
    {
        var start = _baseTime;
        var end = _baseTime.AddDays(3);

        var result = DateTimeRange.Create(start, end);

        result.IsSuccess.Should().BeTrue();
        result.Value.Start.Should().Be(start);
        result.Value.End.Should().Be(end);
        result.Value.TotalDays.Should().Be(3);
    }

    [Fact]
    public void Create_EndBeforeOrEqualToStart_ReturnsFailure()
    {
        var start = _baseTime;
        var end = _baseTime;

        var result = DateTimeRange.Create(start, end);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("datetime_range.invalid_range");
    }

    [Fact]
    public void Constructor_InvalidRange_ThrowsDomainException()
    {
        var start = _baseTime;
        var end = _baseTime.AddHours(-1);

        var act = () => new DateTimeRange(start, end);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be("datetime_range.invalid_range");
    }

    [Theory]
    [InlineData(1, 1)]      // 1 hour -> 1 day
    [InlineData(23, 1)]     // 23 hours -> 1 day
    [InlineData(24, 1)]     // 24 hours -> 1 day
    [InlineData(25, 2)]     // 25 hours -> 2 days
    [InlineData(48, 2)]     // 48 hours -> 2 days
    [InlineData(49, 3)]     // 49 hours -> 3 days
    public void TotalDays_RoundsUpToCeiling(int hours, int expectedDays)
    {
        var start = _baseTime;
        var end = _baseTime.AddHours(hours);

        var range = new DateTimeRange(start, end);

        range.TotalDays.Should().Be(expectedDays);
    }

    [Fact]
    public void Overlaps_WhenRangesOverlap_ReturnsTrue()
    {
        var r1 = new DateTimeRange(_baseTime, _baseTime.AddDays(5));
        var r2 = new DateTimeRange(_baseTime.AddDays(2), _baseTime.AddDays(7));

        r1.Overlaps(r2).Should().BeTrue();
        r2.Overlaps(r1).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_WhenTouchingBoundary_ReturnsFalse()
    {
        var r1 = new DateTimeRange(_baseTime, _baseTime.AddDays(2));
        var r2 = new DateTimeRange(_baseTime.AddDays(2), _baseTime.AddDays(4));

        r1.Overlaps(r2).Should().BeFalse();
        r2.Overlaps(r1).Should().BeFalse();
    }

    [Fact]
    public void Contains_InstantWithinRange_ReturnsTrue()
    {
        var range = new DateTimeRange(_baseTime, _baseTime.AddDays(2));

        range.Contains(_baseTime.AddDays(1)).Should().BeTrue();
        range.Contains(_baseTime).Should().BeTrue();
        range.Contains(_baseTime.AddDays(2)).Should().BeTrue();
        range.Contains(_baseTime.AddDays(-1)).Should().BeFalse();
        range.Contains(_baseTime.AddDays(3)).Should().BeFalse();
    }

    [Fact]
    public void Contains_SubRange_ReturnsTrue()
    {
        var parent = new DateTimeRange(_baseTime, _baseTime.AddDays(10));
        var child = new DateTimeRange(_baseTime.AddDays(2), _baseTime.AddDays(5));
        var outside = new DateTimeRange(_baseTime.AddDays(8), _baseTime.AddDays(12));

        parent.Contains(child).Should().BeTrue();
        parent.Contains(outside).Should().BeFalse();
    }
}
