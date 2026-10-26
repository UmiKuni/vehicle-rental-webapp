using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Bookings.Contracts.Events;

public sealed record BookingCompleted : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid BookingId { get; }
    public Guid VehicleId { get; }
    public Guid CustomerId { get; }
    public Guid OwnerId { get; }

    public BookingCompleted(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid bookingId,
        Guid vehicleId,
        Guid customerId,
        Guid ownerId)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        BookingId = bookingId;
        VehicleId = vehicleId;
        CustomerId = customerId;
        OwnerId = ownerId;
    }
}
