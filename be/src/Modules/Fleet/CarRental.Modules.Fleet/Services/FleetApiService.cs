using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Users.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarRental.Modules.Fleet.Services;

public sealed class FleetApiService : IFleetApi
{
    private readonly FleetDbContext _dbContext;
    private readonly IUsersApi _usersApi;

    public FleetApiService(FleetDbContext dbContext, IUsersApi usersApi)
    {
        _dbContext = dbContext;
        _usersApi = usersApi;
    }

    public async Task<Result<VehicleSummaryDto>> GetVehicleSummaryAsync(Guid vehicleId, CancellationToken ct = default)
    {
        var vehicle = await FindVehicleAsync(vehicleId, ct);
        return vehicle is null
            ? Result.Failure<VehicleSummaryDto>(NotFound())
            : Result.Success(await ToSummaryAsync(vehicle, ct));
    }

    public async Task<Result<IReadOnlyList<VehicleSummaryDto>>> GetPublishedVehiclesAsync(CancellationToken ct = default)
    {
        var vehicles = await _dbContext.Vehicles.AsNoTracking()
            .Where(v => v.Status == VehicleStatus.Published && !v.HiddenByAdmin)
            .OrderBy(v => v.PlateNumber)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<VehicleSummaryDto>>(
            await ToSummariesAsync(vehicles, ct));
    }

    public async Task<Result<IReadOnlyList<VehicleSummaryDto>>> GetOwnerVehiclesAsync(Guid ownerId, CancellationToken ct = default)
    {
        var vehicles = await _dbContext.Vehicles.AsNoTracking()
            .Where(v => v.OwnerId == ownerId)
            .OrderBy(v => v.PlateNumber)
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<VehicleSummaryDto>>(
            await ToSummariesAsync(vehicles, ct));
    }

    public async Task<Result<VehicleSummaryDto>> CreateVehicleAsync(CreateVehicleDto dto, CancellationToken ct = default)
    {
        var ownerResult = await _usersApi.GetProfileAsync(dto.OwnerId, ct);
        if (ownerResult.IsFailure)
        {
            if (ownerResult.Error.Code == "users.not_found")
            {
                return Result.Failure<VehicleSummaryDto>(
                    new Error("fleet.owner_not_found", "Owner user was not found."));
            }

            return Result.Failure<VehicleSummaryDto>(ownerResult.Error);
        }

        var referenceResult = await ValidateReferencesAsync(dto.BrandId, dto.ModelId, ct);
        if (referenceResult.IsFailure)
            return Result.Failure<VehicleSummaryDto>(referenceResult.Error);

        if (string.IsNullOrWhiteSpace(dto.PlateNumber))
            return Result.Failure<VehicleSummaryDto>(
                new Error("fleet.invalid_plate", "Plate number cannot be empty."));

        var normalizedPlateNumber = dto.PlateNumber.Trim().ToUpperInvariant();
        if (await _dbContext.Vehicles.AnyAsync(
                vehicle => vehicle.PlateNumber == normalizedPlateNumber, ct))
        {
            return Result.Failure<VehicleSummaryDto>(
                new Error("fleet.plate_duplicate", "Plate number is already registered."));
        }

        var locationResult = GeoPoint.Create(dto.Latitude, dto.Longitude);
        if (locationResult.IsFailure)
            return Result.Failure<VehicleSummaryDto>(locationResult.Error);

        var vehicleResult = Vehicle.Create(
            dto.OwnerId,
            dto.PlateNumber,
            dto.BrandId,
            dto.ModelId,
            dto.Transmission,
            dto.Seats,
            locationResult.Value,
            dto.Address);
        if (vehicleResult.IsFailure)
            return Result.Failure<VehicleSummaryDto>(vehicleResult.Error);

        var vehicle = vehicleResult.Value;
        await _dbContext.Vehicles.AddAsync(vehicle, ct);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (TryGetUniqueConstraintError(exception, out var error))
        {
            return Result.Failure<VehicleSummaryDto>(error);
        }

        return Result.Success(await ToSummaryAsync(vehicle, ct));
    }

    public async Task<Result<VehicleSummaryDto>> UpdateVehicleAsync(Guid vehicleId, UpdateVehicleDto dto, CancellationToken ct = default)
    {
        var vehicle = await FindVehicleAsync(vehicleId, ct);
        if (vehicle is null)
            return Result.Failure<VehicleSummaryDto>(NotFound());

        var referenceResult = await ValidateReferencesAsync(dto.BrandId, dto.ModelId, ct);
        if (referenceResult.IsFailure)
            return Result.Failure<VehicleSummaryDto>(referenceResult.Error);

        var locationResult = GeoPoint.Create(dto.Latitude, dto.Longitude);
        if (locationResult.IsFailure)
            return Result.Failure<VehicleSummaryDto>(locationResult.Error);

        var result = vehicle.UpdateDetails(
            dto.BrandId,
            dto.ModelId,
            dto.Transmission,
            dto.Seats,
            locationResult.Value,
            dto.Address);
        if (result.IsFailure)
            return Result.Failure<VehicleSummaryDto>(result.Error);

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(await ToSummaryAsync(vehicle, ct));
    }

    public Task<Result> SubmitForApprovalAsync(Guid vehicleId, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.SubmitForApproval(), ct);

    public Task<Result> ApproveAsync(Guid vehicleId, Guid adminId, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.Approve(adminId), ct);

    public Task<Result> RejectAsync(Guid vehicleId, Guid adminId, RejectVehicleDto dto, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.Reject(adminId, dto.Reason), ct);

    public Task<Result> HideAsync(Guid vehicleId, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.Hide(), ct);

    public Task<Result> ShowAsync(Guid vehicleId, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.Show(), ct);

    public Task<Result> UnpublishAsync(Guid vehicleId, Guid adminId, UnpublishVehicleDto dto, CancellationToken ct = default) =>
        ExecuteVehicleActionAsync(vehicleId, vehicle => vehicle.Unpublish(adminId, dto.Reason), ct);

    public async Task<Result<IReadOnlyList<BrandDto>>> GetBrandsAsync(CancellationToken ct = default)
    {
        var brands = await _dbContext.VehicleBrands.AsNoTracking()
            .OrderBy(brand => brand.Name)
            .Select(brand => new BrandDto(brand.Id, brand.Name))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<BrandDto>>(brands);
    }

    public async Task<Result<BrandDto>> GetBrandAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await _dbContext.VehicleBrands.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == brandId, ct);
        return brand is null
            ? Result.Failure<BrandDto>(BrandNotFound())
            : Result.Success(new BrandDto(brand.Id, brand.Name));
    }

    public async Task<Result<BrandDto>> CreateBrandAsync(CreateBrandDto dto, CancellationToken ct = default)
    {
        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<BrandDto>(InvalidName("Brand name is required."));

        var exists = await _dbContext.VehicleBrands.AnyAsync(brand => brand.Name == name, ct);
        if (exists)
            return Result.Failure<BrandDto>(new Error("fleet.brand_duplicate", "Brand name already exists."));

        var brand = new VehicleBrand(Guid.CreateVersion7(), name);
        await _dbContext.VehicleBrands.AddAsync(brand, ct);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(new BrandDto(brand.Id, brand.Name));
    }

    public async Task<Result<BrandDto>> UpdateBrandAsync(Guid brandId, UpdateBrandDto dto, CancellationToken ct = default)
    {
        var brand = await _dbContext.VehicleBrands.FirstOrDefaultAsync(item => item.Id == brandId, ct);
        if (brand is null)
            return Result.Failure<BrandDto>(BrandNotFound());

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<BrandDto>(InvalidName("Brand name is required."));

        var duplicate = await _dbContext.VehicleBrands
            .AnyAsync(item => item.Id != brandId && item.Name == name, ct);
        if (duplicate)
            return Result.Failure<BrandDto>(new Error("fleet.brand_duplicate", "Brand name already exists."));

        brand.UpdateName(name);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(new BrandDto(brand.Id, brand.Name));
    }

    public async Task<Result> DeleteBrandAsync(Guid brandId, CancellationToken ct = default)
    {
        var brand = await _dbContext.VehicleBrands.FirstOrDefaultAsync(item => item.Id == brandId, ct);
        if (brand is null)
            return Result.Failure(BrandNotFound());

        if (await _dbContext.VehicleModels.AnyAsync(model => model.BrandId == brandId, ct))
            return Result.Failure(new Error("fleet.brand_in_use", "Brand cannot be deleted while models reference it."));
        if (await _dbContext.Vehicles.AnyAsync(vehicle => vehicle.BrandId == brandId, ct))
            return Result.Failure(new Error("fleet.brand_in_use", "Brand cannot be deleted while vehicles reference it."));

        _dbContext.VehicleBrands.Remove(brand);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<ModelDto>>> GetModelsAsync(Guid? brandId = null, CancellationToken ct = default)
    {
        IQueryable<VehicleModel> query = _dbContext.VehicleModels.AsNoTracking();
        if (brandId.HasValue)
            query = query.Where(model => model.BrandId == brandId.Value);

        var models = await query.OrderBy(model => model.Name)
            .Select(model => new ModelDto(model.Id, model.BrandId, model.Name))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<ModelDto>>(models);
    }

    public async Task<Result<ModelDto>> GetModelAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _dbContext.VehicleModels.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == modelId, ct);
        return model is null
            ? Result.Failure<ModelDto>(ModelNotFound())
            : Result.Success(new ModelDto(model.Id, model.BrandId, model.Name));
    }

    public async Task<Result<ModelDto>> CreateModelAsync(CreateModelDto dto, CancellationToken ct = default)
    {
        var referenceResult = await ValidateBrandAsync(dto.BrandId, ct);
        if (referenceResult.IsFailure)
            return Result.Failure<ModelDto>(referenceResult.Error);

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ModelDto>(InvalidName("Model name is required."));

        var exists = await _dbContext.VehicleModels
            .AnyAsync(model => model.BrandId == dto.BrandId && model.Name == name, ct);
        if (exists)
            return Result.Failure<ModelDto>(new Error("fleet.model_duplicate", "Model name already exists for this brand."));

        var model = new VehicleModel(Guid.CreateVersion7(), dto.BrandId, name);
        await _dbContext.VehicleModels.AddAsync(model, ct);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(new ModelDto(model.Id, model.BrandId, model.Name));
    }

    public async Task<Result<ModelDto>> UpdateModelAsync(Guid modelId, UpdateModelDto dto, CancellationToken ct = default)
    {
        var model = await _dbContext.VehicleModels.FirstOrDefaultAsync(item => item.Id == modelId, ct);
        if (model is null)
            return Result.Failure<ModelDto>(ModelNotFound());

        var referenceResult = await ValidateBrandAsync(dto.BrandId, ct);
        if (referenceResult.IsFailure)
            return Result.Failure<ModelDto>(referenceResult.Error);

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<ModelDto>(InvalidName("Model name is required."));

        var duplicate = await _dbContext.VehicleModels
            .AnyAsync(item => item.Id != modelId && item.BrandId == dto.BrandId && item.Name == name, ct);
        if (duplicate)
            return Result.Failure<ModelDto>(new Error("fleet.model_duplicate", "Model name already exists for this brand."));

        model.Update(dto.BrandId, name);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success(new ModelDto(model.Id, model.BrandId, model.Name));
    }

    public async Task<Result> DeleteModelAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _dbContext.VehicleModels.FirstOrDefaultAsync(item => item.Id == modelId, ct);
        if (model is null)
            return Result.Failure(ModelNotFound());
        if (await _dbContext.Vehicles.AnyAsync(vehicle => vehicle.ModelId == modelId, ct))
            return Result.Failure(new Error("fleet.model_in_use", "Model cannot be deleted while vehicles reference it."));

        _dbContext.VehicleModels.Remove(model);
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ValidateBrandAsync(Guid brandId, CancellationToken ct) =>
        await _dbContext.VehicleBrands.AnyAsync(brand => brand.Id == brandId, ct)
            ? Result.Success()
            : Result.Failure(BrandNotFound());

    private async Task<Result> ExecuteVehicleActionAsync(
        Guid vehicleId,
        Func<Vehicle, Result> action,
        CancellationToken ct)
    {
        var vehicle = await FindVehicleAsync(vehicleId, ct);
        if (vehicle is null)
            return Result.Failure(NotFound());

        var result = action(vehicle);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    private Task<Vehicle?> FindVehicleAsync(Guid vehicleId, CancellationToken ct) =>
        _dbContext.Vehicles.FirstOrDefaultAsync(
            vehicle => vehicle.Id == VehicleId.From(vehicleId), ct);

    private async Task<Result> ValidateReferencesAsync(
        Guid brandId,
        Guid modelId,
        CancellationToken ct)
    {
        var brandExists = await _dbContext.VehicleBrands
            .AnyAsync(brand => brand.Id == brandId, ct);
        if (!brandExists)
            return Result.Failure(new Error("fleet.brand_not_found", "Vehicle brand was not found."));

        var model = await _dbContext.VehicleModels
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == modelId, ct);
        if (model is null)
            return Result.Failure(new Error("fleet.model_not_found", "Vehicle model was not found."));

        if (model.BrandId != brandId)
            return Result.Failure(new Error(
                "fleet.model_brand_mismatch",
                "Vehicle model does not belong to the selected brand."));

        return Result.Success();
    }

    private async Task<IReadOnlyList<VehicleSummaryDto>> ToSummariesAsync(
        IReadOnlyList<Vehicle> vehicles,
        CancellationToken ct)
    {
        var brandIds = vehicles.Select(vehicle => vehicle.BrandId).Distinct().ToArray();
        var modelIds = vehicles.Select(vehicle => vehicle.ModelId).Distinct().ToArray();
        var brands = await _dbContext.VehicleBrands.AsNoTracking()
            .Where(brand => brandIds.Contains(brand.Id))
            .ToDictionaryAsync(brand => brand.Id, ct);
        var models = await _dbContext.VehicleModels.AsNoTracking()
            .Where(model => modelIds.Contains(model.Id))
            .ToDictionaryAsync(model => model.Id, ct);

        return vehicles.Select(vehicle => ToSummary(
            vehicle,
            brands.TryGetValue(vehicle.BrandId, out var brand) ? brand.Name : string.Empty,
            models.TryGetValue(vehicle.ModelId, out var model) ? model.Name : string.Empty)).ToArray();
    }

    private async Task<VehicleSummaryDto> ToSummaryAsync(Vehicle vehicle, CancellationToken ct)
    {
        var brand = await _dbContext.VehicleBrands.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vehicle.BrandId, ct);
        var model = await _dbContext.VehicleModels.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vehicle.ModelId, ct);
        return ToSummary(vehicle, brand?.Name ?? string.Empty, model?.Name ?? string.Empty);
    }

    private static VehicleSummaryDto ToSummary(Vehicle vehicle, string brandName, string modelName)
    {
        return new VehicleSummaryDto(
            vehicle.Id.Value,
            vehicle.OwnerId,
            vehicle.Status.ToString(),
            vehicle.BrandId,
            brandName,
            vehicle.ModelId,
            modelName,
            vehicle.PlateNumber,
            vehicle.Transmission.ToString(),
            vehicle.Seats,
            vehicle.Images.FirstOrDefault(image => image.IsPrimary)?.StorageKey,
            vehicle.Location,
            vehicle.Address);
    }

    private static Error NotFound() =>
        new("fleet.not_found", "Vehicle not found.");

    private static Error BrandNotFound() =>
        new("fleet.brand_not_found", "Vehicle brand was not found.");

    private static Error ModelNotFound() =>
        new("fleet.model_not_found", "Vehicle model was not found.");

    private static Error InvalidName(string message) =>
        new("fleet.invalid_name", message);

    private static bool TryGetUniqueConstraintError(
        DbUpdateException exception,
        out Error error)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            error = postgresException.ConstraintName switch
            {
                "IX_vehicles_plate_number" => new Error(
                    "fleet.plate_duplicate",
                    "Plate number is already registered."),
                "IX_vehicle_brands_name" => new Error(
                    "fleet.brand_duplicate",
                    "Brand name already exists."),
                _ => Error.None
            };

            return error != Error.None;
        }

        error = Error.None;
        return false;
    }
}
