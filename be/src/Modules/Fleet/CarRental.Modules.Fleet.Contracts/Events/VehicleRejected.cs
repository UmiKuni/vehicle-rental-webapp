using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehicleRejected(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VehicleId,
    Guid OwnerId,
    string Reason) : IntegrationEvent(EventId, OccurredAt);
