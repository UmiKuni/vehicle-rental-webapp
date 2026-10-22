using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Users.Contracts.Events;

public sealed record DriverLicenseReviewed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    bool Approved,
    string? Reason) : IntegrationEvent(EventId, OccurredAt);
