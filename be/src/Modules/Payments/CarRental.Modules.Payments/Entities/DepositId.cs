namespace CarRental.Modules.Payments.Entities;

public readonly record struct DepositId(Guid Value)
{
    public static DepositId New() => new(Guid.CreateVersion7());
    public static DepositId From(Guid value) => new(value);
    public static implicit operator Guid(DepositId id) => id.Value;
    public static implicit operator DepositId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
