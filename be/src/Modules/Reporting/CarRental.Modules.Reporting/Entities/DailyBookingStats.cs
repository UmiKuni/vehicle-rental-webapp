using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reporting.Entities;

/// <summary>
/// Pre-aggregated daily booking and revenue projection for platform analytics and executive dashboards.
/// Uses UTC+7 date bucketing to align with local business days.
/// </summary>
public sealed class DailyBookingStats : Entity<DateOnly>
{
    public static readonly TimeSpan PlatformTimeZoneOffset = TimeSpan.FromHours(7);

    public DateOnly Date => Id;
    public int TotalBookings { get; private set; }
    public decimal TotalRevenue { get; private set; }
    public int CancellationCount { get; private set; }

    private DailyBookingStats()
    {
    }

    private DailyBookingStats(DateOnly date) : base(date)
    {
        TotalBookings = 0;
        TotalRevenue = 0m;
        CancellationCount = 0;
    }

    public static DailyBookingStats Create(DateOnly date)
    {
        return new DailyBookingStats(date);
    }

    public static DailyBookingStats ForTimestamp(DateTimeOffset timestamp)
    {
        return new DailyBookingStats(ToBucketDate(timestamp));
    }

    public static DateOnly ToBucketDate(DateTimeOffset timestamp)
    {
        var localTime = timestamp.ToOffset(PlatformTimeZoneOffset);
        return DateOnly.FromDateTime(localTime.DateTime);
    }

    public bool ApplyBookingRequested(DateTimeOffset occurredAt)
    {
        if (ToBucketDate(occurredAt) != Id)
        {
            return false;
        }

        TotalBookings++;
        return true;
    }

    public bool ApplyBookingCancelled(DateTimeOffset occurredAt)
    {
        if (ToBucketDate(occurredAt) != Id)
        {
            return false;
        }

        CancellationCount++;
        return true;
    }

    public bool ApplyPaymentSucceeded(decimal amount, DateTimeOffset occurredAt)
    {
        if (ToBucketDate(occurredAt) != Id || amount <= 0m)
        {
            return false;
        }

        TotalRevenue += amount;
        return true;
    }
}
