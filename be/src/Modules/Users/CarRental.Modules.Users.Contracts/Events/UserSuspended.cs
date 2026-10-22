using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Users.Contracts.Events;

public sealed record UserSuspended(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    string Reason) : IntegrationEvent(EventId, OccurredAt);
