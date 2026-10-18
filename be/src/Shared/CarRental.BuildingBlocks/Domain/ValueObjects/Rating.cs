using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a star rating from 1 to 5.
/// Maps to smallint in PostgreSQL.
/// </summary>
public sealed record Rating : ValueObject, IComparable<Rating>
{
    public const byte MinValue = 1;
    public const byte MaxValue = 5;

    public byte Value { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private Rating()
    {
        Value = MinValue;
    }

    public Rating(byte value)
    {
        Guard.Against(value < MinValue || value > MaxValue, "rating.out_of_range", $"Rating must be between {MinValue} and {MaxValue}.");
        Value = value;
    }

    public static Result<Rating> Create(byte value)
    {
        if (value < MinValue || value > MaxValue)
        {
            return Result<Rating>.Failure(
                new Error("rating.out_of_range", $"Rating must be between {MinValue} and {MaxValue}."));
        }

        return Result<Rating>.Success(new Rating(value));
    }

    public static implicit operator byte(Rating rating) => rating.Value;

    public int CompareTo(Rating? other) => other is null ? 1 : Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
