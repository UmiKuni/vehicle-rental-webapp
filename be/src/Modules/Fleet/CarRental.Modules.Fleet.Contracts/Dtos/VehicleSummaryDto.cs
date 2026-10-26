using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Fleet.Contracts.Dtos;

public sealed record VehicleSummaryDto(
    Guid VehicleId,
    Guid OwnerId,
    string Status,
    Guid BrandId,
    string BrandName,
    Guid ModelId,
    string ModelName,
    string PlateNumber,
    string Transmission,
    int Seats,
    string? PrimaryImageKey,
    GeoPoint Location,
    string Address);
