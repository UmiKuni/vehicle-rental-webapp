using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Fleet.Entities;

public class VehicleModel : Entity<Guid>
{
    public Guid BrandId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    private VehicleModel() { }

    public VehicleModel(Guid id, Guid brandId, string name)
    {
        Id = id;
        BrandId = brandId;
        Name = name;
    }
}
