using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Users.Contracts.Events;

public sealed record UserRegistered(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Email,
    string FullName) : IntegrationEvent(EventId, OccurredAt);
