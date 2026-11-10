using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Pricing.Entities;

public class PricingPlanVersion : Entity<Guid>
{
    public Guid VehicleId { get; private set; }
    public string Config { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private PricingPlanVersion() { }

    public PricingPlanVersion(Guid id, Guid vehicleId, string config, DateTimeOffset createdAt)
    {
        Id = id;
        VehicleId = vehicleId;
        Config = config;
        CreatedAt = createdAt;
    }
}
