using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts.Dtos;

namespace CarRental.Modules.Fleet.Contracts;

public interface IFleetApi
{
    Task<Result<VehicleSummaryDto>> GetVehicleSummaryAsync(Guid vehicleId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<VehicleSummaryDto>>> GetPublishedVehiclesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<VehicleSummaryDto>>> GetOwnerVehiclesAsync(Guid ownerId, CancellationToken ct = default);
    Task<Result<VehicleSummaryDto>> CreateVehicleAsync(CreateVehicleDto dto, CancellationToken ct = default);
    Task<Result<VehicleSummaryDto>> UpdateVehicleAsync(Guid vehicleId, UpdateVehicleDto dto, CancellationToken ct = default);
    Task<Result> SubmitForApprovalAsync(Guid vehicleId, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid vehicleId, Guid adminId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid vehicleId, Guid adminId, RejectVehicleDto dto, CancellationToken ct = default);
    Task<Result> HideAsync(Guid vehicleId, CancellationToken ct = default);
    Task<Result> ShowAsync(Guid vehicleId, CancellationToken ct = default);
    Task<Result> UnpublishAsync(Guid vehicleId, Guid adminId, UnpublishVehicleDto dto, CancellationToken ct = default);
    Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(CancellationToken ct = default);
    Task<Result<BrandDto>> GetBrandAsync(Guid brandId, CancellationToken ct = default);
    Task<Result<BrandDto>> CreateBrandAsync(CreateBrandDto dto, CancellationToken ct = default);
    Task<Result<BrandDto>> UpdateBrandAsync(Guid brandId, UpdateBrandDto dto, CancellationToken ct = default);
    Task<Result> DeleteBrandAsync(Guid brandId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ModelDto>>> GetModelsAsync(Guid? brandId = null, CancellationToken ct = default);
    Task<Result<ModelDto>> GetModelAsync(Guid modelId, CancellationToken ct = default);
    Task<Result<ModelDto>> CreateModelAsync(CreateModelDto dto, CancellationToken ct = default);
    Task<Result<ModelDto>> UpdateModelAsync(Guid modelId, UpdateModelDto dto, CancellationToken ct = default);
    Task<Result> DeleteModelAsync(Guid modelId, CancellationToken ct = default);
}
