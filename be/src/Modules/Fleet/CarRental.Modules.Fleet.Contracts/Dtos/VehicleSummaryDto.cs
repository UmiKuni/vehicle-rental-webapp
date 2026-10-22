using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Fleet.Contracts.Dtos;

public sealed record VehicleSummaryDto(
    Guid VehicleId,
    Guid OwnerId,
    string Status,
    string Title,
    string Brand,
    string Model,
    string? PrimaryPhotoUrl,
    Address Address);
