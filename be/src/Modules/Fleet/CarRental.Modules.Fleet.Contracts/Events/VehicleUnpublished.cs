using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehicleUnpublished : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid VehicleId { get; }
    public Guid OwnerId { get; }
    public string Reason { get; }

    public VehicleUnpublished(
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid vehicleId,
        Guid ownerId,
        string reason)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        VehicleId = vehicleId;
        OwnerId = ownerId;
        Reason = reason;
    }
}
