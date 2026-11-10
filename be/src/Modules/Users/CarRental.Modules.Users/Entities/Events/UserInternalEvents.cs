using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Users.Entities.Events;

public sealed record UserBecameOwnerDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId) : DomainEvent(EventId, OccurredAt);

public sealed record UserReinstatedDomainEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId) : DomainEvent(EventId, OccurredAt);
