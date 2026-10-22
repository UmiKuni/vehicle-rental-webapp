using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a physical address.
/// Used for pickup and return locations, user addresses, and branch offices.
/// </summary>
public sealed record Address : ValueObject
{
    public const string DefaultCountry = "VN";

    public string Line { get; init; }
    public string Ward { get; init; }
    public string District { get; init; }
    public string City { get; init; }
    public string Country { get; init; }
    public GeoPoint? Location { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private Address()
    {
        Line = string.Empty;
        Ward = string.Empty;
        District = string.Empty;
        City = string.Empty;
        Country = DefaultCountry;
    }

    public Address(
        string line,
        string ward,
        string district,
        string city,
        string country = DefaultCountry,
        GeoPoint? location = null)
    {
        Guard.AgainstNullOrWhiteSpace(line, "address.invalid_line", "Address line is required.");
        Guard.AgainstNullOrWhiteSpace(city, "address.invalid_city", "City is required.");

        Line = line.Trim();
        Ward = ward?.Trim() ?? string.Empty;
        District = district?.Trim() ?? string.Empty;
        City = city.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? DefaultCountry : country.Trim().ToUpperInvariant();
        Location = location;
    }

    public static Result<Address> Create(
        string line,
        string ward,
        string district,
        string city,
        string country = DefaultCountry,
        GeoPoint? location = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return Result<Address>.Failure(new Error("address.invalid_line", "Address line is required."));
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return Result<Address>.Failure(new Error("address.invalid_city", "City is required."));
        }

        return Result<Address>.Success(new Address(line, ward, district, city, country, location));
    }

    public override string ToString()
    {
        var parts = new List<string> { Line };
        if (!string.IsNullOrWhiteSpace(Ward)) parts.Add(Ward);
        if (!string.IsNullOrWhiteSpace(District)) parts.Add(District);
        parts.Add(City);
        if (!string.IsNullOrWhiteSpace(Country)) parts.Add(Country);

        return string.Join(", ", parts);
    }
}
