namespace CarRental.Modules.Support.Entities;

public readonly record struct TicketId(Guid Value)
{
    public static TicketId New() => new(Guid.CreateVersion7());
    public static TicketId From(Guid value) => new(value);
    public static implicit operator Guid(TicketId id) => id.Value;
    public static implicit operator TicketId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
