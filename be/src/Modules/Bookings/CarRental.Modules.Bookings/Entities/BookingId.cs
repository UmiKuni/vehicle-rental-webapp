namespace CarRental.Modules.Bookings.Entities;

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.CreateVersion7());
    public static BookingId From(Guid value) => new(value);
    public static implicit operator Guid(BookingId id) => id.Value;
    public static implicit operator BookingId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
