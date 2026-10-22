using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Fleet.Contracts.Events;

public sealed record VehicleListingChanged(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid VehicleId,
    string Title,
    string Brand,
    string Model,
    int Year,
    double Lat,
    double Lon,
    string City,
    string District,
    string? PrimaryPhotoUrl) : IntegrationEvent(EventId, OccurredAt);
