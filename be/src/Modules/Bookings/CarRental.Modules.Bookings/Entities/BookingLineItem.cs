using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Bookings.Contracts.Enums;

namespace CarRental.Modules.Bookings.Entities;

public class BookingLineItem : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public BookingLineItemType Type { get; private set; }
    public Money Amount { get; private set; } = null!;

    private BookingLineItem() { }

    public BookingLineItem(Guid id, Guid bookingId, BookingLineItemType type, Money amount)
    {
        Id = id;
        BookingId = bookingId;
        Type = type;
        Amount = amount ?? throw new ArgumentNullException(nameof(amount));
    }
}
