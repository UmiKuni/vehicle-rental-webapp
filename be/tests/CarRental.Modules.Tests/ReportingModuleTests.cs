using CarRental.Modules.Reporting.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class ReportingModuleTests
{
    // UTC 2026-10-21 03:00:00 -> in UTC+7 is 2026-10-21 10:00:00
    private static readonly DateTimeOffset TodayUtc = new DateTimeOffset(2026, 10, 21, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly TodayBucket = new DateOnly(2026, 10, 21);

    [Fact]
    public void ToBucketDate_ShouldConvertToUtcPlus7Bucket()
    {
        // 2026-10-20 20:00:00 UTC -> in UTC+7 is 2026-10-21 03:00:00 -> Date is 2026-10-21
        var lateNightUtc = new DateTimeOffset(2026, 10, 20, 20, 0, 0, TimeSpan.Zero);
        var bucket = DailyBookingStats.ToBucketDate(lateNightUtc);

        bucket.Should().Be(new DateOnly(2026, 10, 21));
    }

    [Fact]
    public void ApplyBookingRequested_WithMatchingDateBucket_ShouldIncrementTotalBookings()
    {
        var stats = DailyBookingStats.Create(TodayBucket);

        var applied = stats.ApplyBookingRequested(TodayUtc);

        applied.Should().BeTrue();
        stats.TotalBookings.Should().Be(1);
        stats.TotalRevenue.Should().Be(0m);
        stats.CancellationCount.Should().Be(0);
    }

    [Fact]
    public void ApplyBookingRequested_WithDifferentDateBucket_ShouldReturnFalseAndNotIncrement()
    {
        var stats = DailyBookingStats.Create(TodayBucket);
        var yesterdayUtc = TodayUtc.AddDays(-1);

        var applied = stats.ApplyBookingRequested(yesterdayUtc);

        applied.Should().BeFalse();
        stats.TotalBookings.Should().Be(0);
    }

    [Fact]
    public void ApplyBookingCancelled_WithMatchingDateBucket_ShouldIncrementCancellationCount()
    {
        var stats = DailyBookingStats.Create(TodayBucket);

        var applied = stats.ApplyBookingCancelled(TodayUtc);

        applied.Should().BeTrue();
        stats.CancellationCount.Should().Be(1);
    }

    [Fact]
    public void ApplyBookingCancelled_WithDifferentDateBucket_ShouldReturnFalse()
    {
        var stats = DailyBookingStats.Create(TodayBucket);
        var tomorrowUtc = TodayUtc.AddDays(1);

        var applied = stats.ApplyBookingCancelled(tomorrowUtc);

        applied.Should().BeFalse();
        stats.CancellationCount.Should().Be(0);
    }

    [Fact]
    public void ApplyPaymentSucceeded_WithMatchingDateBucket_ShouldAddRevenue()
    {
        var stats = DailyBookingStats.Create(TodayBucket);

        var applied1 = stats.ApplyPaymentSucceeded(1_500_000m, TodayUtc);
        var applied2 = stats.ApplyPaymentSucceeded(800_000m, TodayUtc.AddHours(2));

        applied1.Should().BeTrue();
        applied2.Should().BeTrue();
        stats.TotalRevenue.Should().Be(2_300_000m);
    }

    [Fact]
    public void ApplyPaymentSucceeded_WithZeroOrNegativeAmount_ShouldReturnFalse()
    {
        var stats = DailyBookingStats.Create(TodayBucket);

        var zeroApplied = stats.ApplyPaymentSucceeded(0m, TodayUtc);
        var negativeApplied = stats.ApplyPaymentSucceeded(-100m, TodayUtc);

        zeroApplied.Should().BeFalse();
        negativeApplied.Should().BeFalse();
        stats.TotalRevenue.Should().Be(0m);
    }

    [Fact]
    public void ApplyPaymentSucceeded_WithDifferentDateBucket_ShouldReturnFalse()
    {
        var stats = DailyBookingStats.Create(TodayBucket);
        var yesterdayUtc = TodayUtc.AddDays(-1);

        var applied = stats.ApplyPaymentSucceeded(1_000_000m, yesterdayUtc);

        applied.Should().BeFalse();
        stats.TotalRevenue.Should().Be(0m);
    }

    [Fact]
    public void ProcessedEvent_Create_ShouldInitializeWithEventIdAndTimestamp()
    {
        var eventId = Guid.NewGuid();
        var processedAt = DateTimeOffset.UtcNow;

        var processed = ProcessedEvent.Create(eventId, processedAt);

        processed.Id.Should().Be(eventId);
        processed.EventId.Should().Be(eventId);
        processed.ProcessedAt.Should().Be(processedAt);
    }
}
