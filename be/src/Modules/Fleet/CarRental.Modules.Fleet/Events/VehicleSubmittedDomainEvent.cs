using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Fleet.ValueObjects;

namespace CarRental.Modules.Fleet.Events;

public sealed record VehicleSubmittedDomainEvent(
    VehicleId VehicleId,
    Guid OwnerId,
    DateTimeOffset OccurredAt) : DomainEvent(Guid.CreateVersion7(), OccurredAt);
