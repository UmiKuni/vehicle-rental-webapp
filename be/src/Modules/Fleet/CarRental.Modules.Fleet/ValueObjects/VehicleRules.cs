using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Fleet.ValueObjects;

public sealed record VehicleRules : ValueObject
{
    public int MinRenterAge { get; }
    public int MinLicenseYears { get; }
    public bool AllowDelivery { get; }
    public string? FuelPolicy { get; }
    public string? Notes { get; }

    public VehicleRules(
        int minRenterAge = 21,
        int minLicenseYears = 1,
        bool allowDelivery = false,
        string? fuelPolicy = null,
        string? notes = null)
    {
        Guard.Against(minRenterAge < 18, "rules.invalid_age", "Minimum renter age must be at least 18.");
        Guard.Against(minLicenseYears < 0, "rules.invalid_license_years", "Minimum license years cannot be negative.");

        MinRenterAge = minRenterAge;
        MinLicenseYears = minLicenseYears;
        AllowDelivery = allowDelivery;
        FuelPolicy = fuelPolicy?.Trim();
        Notes = notes?.Trim();
    }

    private VehicleRules()
    {
        MinRenterAge = 21;
        MinLicenseYears = 1;
    }
}
