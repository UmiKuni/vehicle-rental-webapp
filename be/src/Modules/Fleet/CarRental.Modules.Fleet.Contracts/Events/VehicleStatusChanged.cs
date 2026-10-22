using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehicleStatusChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VehicleId,
    string NewStatus) : IntegrationEvent(EventId, OccurredAt);
