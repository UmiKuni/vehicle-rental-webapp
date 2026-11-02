using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Contracts.Enums;

namespace CarRental.Modules.Fleet.Contracts.Dtos;

public sealed record CreateVehicleDto(
    Guid OwnerId,
    string PlateNumber,
    Guid BrandId,
    Guid ModelId,
    TransmissionType Transmission,
    int Seats,
    double Latitude,
    double Longitude,
    string Address);

public sealed record UpdateVehicleDto(
    Guid BrandId,
    Guid ModelId,
    TransmissionType Transmission,
    int Seats,
    double Latitude,
    double Longitude,
    string Address);

public sealed record RejectVehicleDto(string Reason);

public sealed record UnpublishVehicleDto(string Reason);

public sealed record CreateBrandDto(string Name);

public sealed record UpdateBrandDto(string Name);

public sealed record BrandDto(Guid BrandId, string Name);

public sealed record CreateModelDto(Guid BrandId, string Name);

public sealed record UpdateModelDto(Guid BrandId, string Name);

public sealed record ModelDto(Guid ModelId, Guid BrandId, string Name);
