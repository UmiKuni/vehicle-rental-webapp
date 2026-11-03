using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Fleet.Entities;

public class VehicleImage : Entity<Guid>
{
    public VehicleId VehicleId { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    private VehicleImage() { }

    internal VehicleImage(Guid id, VehicleId vehicleId, string storageKey, bool isPrimary)
    {
        Id = id;
        VehicleId = vehicleId;
        StorageKey = storageKey;
        IsPrimary = isPrimary;
    }

    internal void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
    }
}
