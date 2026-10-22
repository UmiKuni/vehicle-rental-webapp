namespace CarRental.Modules.Handover.Entities;

public readonly record struct HandoverId(Guid Value)
{
    public static HandoverId New() => new(Guid.CreateVersion7());
    public static HandoverId From(Guid value) => new(value);
    public static implicit operator Guid(HandoverId id) => id.Value;
    public static implicit operator HandoverId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
