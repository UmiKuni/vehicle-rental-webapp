using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts.Enums;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Pricing.Services;

public sealed class PricingApiService : IPricingApi
{
    private readonly PricingDbContext _dbContext;
    private readonly IFleetApi _fleetApi;

    public PricingApiService(PricingDbContext dbContext, IFleetApi fleetApi)
    {
        _dbContext = dbContext;
        _fleetApi = fleetApi;
    }

    public async Task<Result<PricingPlanDto>> GetPricingAsync(
        Guid vehicleId,
        CancellationToken ct = default)
    {
        var plan = await _dbContext.PricingPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.VehicleId == vehicleId, ct);

        return plan is null
            ? Result.Failure<PricingPlanDto>(NotFound())
            : Result.Success(ToDto(plan));
    }

    public async Task<Result<QuoteDto>> GetQuoteAsync(
        Guid vehicleId,
        DateTimeRange period,
        CancellationToken ct = default)
    {
        var plan = await _dbContext.PricingPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.VehicleId == vehicleId, ct);

        if (plan is null)
            return Result.Failure<QuoteDto>(NotFound());

        var quoteResult = plan.CalculateQuote(period);
        return quoteResult.IsFailure
            ? Result.Failure<QuoteDto>(quoteResult.Error)
            : Result.Success(quoteResult.Value.ToDto());
    }

    public async Task<Result<PricingPlanDto>> ConfigurePricingAsync(
        Guid vehicleId,
        ConfigurePricingDto dto,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<PricingPlanDto>(
                new Error("pricing.invalid_vehicle", "Vehicle ID is required."));

        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
        if (vehicleResult.IsFailure)
            return Result.Failure<PricingPlanDto>(vehicleResult.Error);

        if (vehicleResult.Value.OwnerId != dto.OwnerId)
            return Result.Failure<PricingPlanDto>(
                new Error("pricing.owner_required", "Only the vehicle owner can configure pricing."));

        var now = DateTimeOffset.UtcNow;
        var plan = await _dbContext.PricingPlans
            .SingleOrDefaultAsync(p => p.VehicleId == vehicleId, ct);

        if (plan is null)
        {
            var createResult = PricingPlan.Create(
                vehicleId,
                dto.WeekdayRate,
                dto.WeekendRate,
                dto.DepositAmount,
                dto.KmLimit,
                now,
                dto.HourlyRate);
            if (createResult.IsFailure)
                return Result.Failure<PricingPlanDto>(createResult.Error);

            plan = createResult.Value;
            _dbContext.PricingPlans.Add(plan);
        }
        else
        {
            var updateResult = plan.Update(
                dto.WeekdayRate,
                dto.WeekendRate,
                dto.DepositAmount,
                dto.KmLimit,
                now,
                dto.HourlyRate);
            if (updateResult.IsFailure)
                return Result.Failure<PricingPlanDto>(updateResult.Error);
        }

        var feeResult = ApplyFeeRule(plan, FeeType.OverKm, dto.OverKmFee, now);
        if (feeResult.IsFailure)
            return Result.Failure<PricingPlanDto>(feeResult.Error);

        feeResult = ApplyFeeRule(plan, FeeType.Cleaning, dto.CleaningFee, now);
        if (feeResult.IsFailure)
            return Result.Failure<PricingPlanDto>(feeResult.Error);

        await _dbContext.SaveChangesAsync(ct);
        _dbContext.PricingPlanVersions.Add(plan.CreateVersionSnapshot(now));
        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(ToDto(plan));
    }

    private static Result ApplyFeeRule(
        PricingPlan plan,
        FeeType type,
        Money? amount,
        DateTimeOffset now)
    {
        return amount is null
            ? plan.RemoveFeeRule(type, now)
            : plan.SetFeeRule(type, amount, now);
    }

    private static PricingPlanDto ToDto(PricingPlan plan) =>
        new(
            plan.VehicleId,
            plan.HourlyRate,
            plan.WeekdayRate,
            plan.WeekendRate,
            plan.DepositAmount,
            plan.KmLimit,
            plan.FeeRules
                .OrderBy(f => f.Type)
                .Select(f => new FeeRuleDto(f.Type.ToString(), f.Amount))
                .ToList());

    private static Error NotFound() =>
        new("pricing.not_found", "Pricing plan was not found for the vehicle.");
}
