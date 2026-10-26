namespace CarRental.Modules.Fleet.Entities;

public readonly record struct VehicleId(Guid Value)
{
    public static VehicleId New() => new(Guid.CreateVersion7());
    public static VehicleId From(Guid value) => new(value);
    public static implicit operator Guid(VehicleId id) => id.Value;
    public static implicit operator VehicleId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
