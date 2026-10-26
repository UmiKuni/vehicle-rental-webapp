using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts.Dtos;

namespace CarRental.Modules.Fleet.Contracts;

public interface IFleetApi
{
    Task<Result<VehicleSummaryDto>> GetVehicleSummaryAsync(Guid vehicleId, CancellationToken ct = default);
}
