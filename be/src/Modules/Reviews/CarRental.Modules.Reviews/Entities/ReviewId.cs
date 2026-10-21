namespace CarRental.Modules.Reviews.Entities;

public readonly record struct ReviewId(Guid Value)
{
    public static ReviewId New() => new(Guid.CreateVersion7());
    public static ReviewId From(Guid value) => new(value);
    public static implicit operator Guid(ReviewId id) => id.Value;
    public static implicit operator ReviewId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
