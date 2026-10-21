using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingPaid : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid CustomerId { get; }
    public Guid OwnerId { get; }
    public Guid VehicleId { get; }
    public DateTimeRange Period { get; }

    public BookingPaid(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid customerId,
        Guid ownerId,
        Guid vehicleId,
        DateTimeRange period)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        CustomerId = customerId;
        OwnerId = ownerId;
        VehicleId = vehicleId;
        Period = period;
    }
}
