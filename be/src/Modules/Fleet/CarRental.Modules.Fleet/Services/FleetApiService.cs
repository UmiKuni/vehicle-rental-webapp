using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Fleet.Data.Repositories;
using CarRental.Modules.Fleet.ValueObjects;

namespace CarRental.Modules.Fleet.Services;

public class FleetApiService : IFleetApi
{
    private readonly IVehicleRepository _vehicleRepository;

    public FleetApiService(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<VehicleSummaryDto>> GetVehicleSummaryAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(new VehicleId(vehicleId), ct);
        if (vehicle == null)
        {
            return Result<VehicleSummaryDto>.Failure(new Error("vehicle.not_found", $"Vehicle with ID '{vehicleId}' was not found."));
        }

        var primaryPhoto = vehicle.Photos.FirstOrDefault(p => p.IsPrimary)?.Photo.Url
            ?? vehicle.Photos.FirstOrDefault()?.Photo.Url;

        var summary = new VehicleSummaryDto(
            vehicle.Id.Value,
            vehicle.OwnerId,
            vehicle.Status.ToString(),
            vehicle.Title,
            vehicle.Brand,
            vehicle.Model,
            primaryPhoto,
            vehicle.Address);

        return Result<VehicleSummaryDto>.Success(summary);
    }
}
