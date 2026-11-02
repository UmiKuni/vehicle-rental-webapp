using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Fleet.Entities;

public class VehicleBrand : Entity<Guid>
{
    public string Name { get; private set; } = string.Empty;

    private VehicleBrand() { }

    public VehicleBrand(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public void UpdateName(string name)
    {
        Name = name.Trim();
    }
}
