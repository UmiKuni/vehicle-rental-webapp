namespace CarRental.Modules.Users.ValueObjects;

public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.CreateVersion7());
    public static UserId Empty => new(Guid.Empty);

    public static implicit operator Guid(UserId userId) => userId.Value;
    public static explicit operator UserId(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
