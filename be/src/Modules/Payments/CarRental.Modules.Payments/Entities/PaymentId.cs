namespace CarRental.Modules.Payments.Entities;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.CreateVersion7());
    public static PaymentId From(Guid value) => new(value);
    public static implicit operator Guid(PaymentId id) => id.Value;
    public static implicit operator PaymentId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
