using CarRental.BuildingBlocks.Domain;
using CarRental.Modules.Users.ValueObjects;

namespace CarRental.Modules.Users.Events;

public sealed record UserBecameOwnerDomainEvent(
    UserId UserId,
    DateTimeOffset OccurredAt) : DomainEvent(Guid.CreateVersion7(), OccurredAt);
