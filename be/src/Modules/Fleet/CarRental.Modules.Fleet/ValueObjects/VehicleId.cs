namespace CarRental.Modules.Fleet.ValueObjects;

public readonly record struct VehicleId(Guid Value)
{
    public static VehicleId New() => new(Guid.CreateVersion7());
    public static VehicleId Empty => new(Guid.Empty);

    public static implicit operator Guid(VehicleId vehicleId) => vehicleId.Value;
    public static explicit operator VehicleId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
