using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingRequested : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid CustomerId { get; }
    public Guid OwnerId { get; }
    public Guid VehicleId { get; }
    public DateTimeRange Period { get; }
    public Money Total { get; }
    public Money DepositAmount { get; }

    public BookingRequested(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid customerId,
        Guid ownerId,
        Guid vehicleId,
        DateTimeRange period,
        Money total,
        Money depositAmount)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        CustomerId = customerId;
        OwnerId = ownerId;
        VehicleId = vehicleId;
        Period = period;
        Total = total;
        DepositAmount = depositAmount;
    }
}
