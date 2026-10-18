using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a time range with explicit start and end instants in UTC.
/// Used for rental periods, hold deadlines, and availability blocks.
/// </summary>
public sealed record DateTimeRange : ValueObject
{
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset End { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private DateTimeRange()
    {
    }

    public DateTimeRange(DateTimeOffset start, DateTimeOffset end)
    {
        Guard.Against(end <= start, "datetime_range.invalid_range", "End date must be strictly after start date.");

        Start = start;
        End = end;
    }

    public static Result<DateTimeRange> Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            return Result<DateTimeRange>.Failure(
                new Error("datetime_range.invalid_range", "End date must be strictly after start date."));
        }

        return Result<DateTimeRange>.Success(new DateTimeRange(start, end));
    }

    /// <summary>
    /// Exact duration between start and end.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Total rental days, rounded up to the nearest whole day.
    /// </summary>
    public int TotalDays => (int)Math.Ceiling(Duration.TotalDays);

    /// <summary>
    /// Determines whether this range overlaps with another range.
    /// Intervals overlap when Start &lt; other.End and End &gt; other.Start.
    /// </summary>
    public bool Overlaps(DateTimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start < other.End && End > other.Start;
    }

    /// <summary>
    /// Determines whether the specified instant falls within this range [Start, End].
    /// </summary>
    public bool Contains(DateTimeOffset instant) => instant >= Start && instant <= End;

    /// <summary>
    /// Determines whether this range completely encloses another range.
    /// </summary>
    public bool Contains(DateTimeRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other.Start >= Start && other.End <= End;
    }

    public override string ToString() => $"{Start:O} - {End:O}";
}
