using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehicleApproved(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VehicleId,
    Guid OwnerId,
    Guid AdminId) : IntegrationEvent(EventId, OccurredAt);
