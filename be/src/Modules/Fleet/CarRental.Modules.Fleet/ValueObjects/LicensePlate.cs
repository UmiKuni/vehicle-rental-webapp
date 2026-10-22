using System.Text.RegularExpressions;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Fleet.ValueObjects;

public sealed partial record LicensePlate : ValueObject
{
    private static readonly Regex PlateRegex = MyPlateRegex();

    public string Value { get; }

    public LicensePlate(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value, nameof(value), "License plate is required.");
        var normalized = Normalize(value);
        Guard.Against(!PlateRegex.IsMatch(normalized), "license_plate.invalid_format", $"Invalid license plate format: '{value}'.");
        Value = normalized;
    }

    private LicensePlate()
    {
        Value = string.Empty;
    }

    public static Result<LicensePlate> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result<LicensePlate>.Failure(new Error("license_plate.empty", "License plate is required."));
        }

        var normalized = Normalize(value);
        if (!PlateRegex.IsMatch(normalized))
        {
            return Result<LicensePlate>.Failure(new Error("license_plate.invalid_format", $"Invalid license plate format: '{value}'."));
        }

        return Result<LicensePlate>.Success(new LicensePlate(normalized));
    }

    private static string Normalize(string raw)
    {
        return raw.Trim().ToUpperInvariant().Replace(" ", "");
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[0-9]{2}[A-Z]{1,2}-?[0-9]{3,5}(\.[0-9]{2})?$")]
    private static partial Regex MyPlateRegex();
}
