using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts.Enums;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Entities;
using CarRental.Modules.Pricing.Errors;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Pricing.Services;

internal sealed class PricingApiService : IPricingApi
{
    private readonly PricingDbContext _dbContext;
    private readonly IFleetApi _fleetApi;
    private readonly IEventOutbox _eventOutbox;
    private readonly TimeProvider _clock;

    public PricingApiService(
        PricingDbContext dbContext,
        IFleetApi fleetApi,
        IEventOutbox eventOutbox,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _fleetApi = fleetApi;
        _eventOutbox = eventOutbox;
        _clock = clock;
    }

    public async Task<Result<PricingPlanDto>> GetPricingAsync(
        Guid vehicleId,
        CancellationToken ct = default)
    {
        var planResult = await GetPlanOrFailAsync(vehicleId, ct);
        if (planResult.IsFailure)
            return planResult.Error;

        return ToDto(planResult.Value);
    }

    public async Task<Result<QuoteDto>> GetQuoteAsync(
        Guid vehicleId,
        DateTimeRange period,
        CancellationToken ct = default)
    {
        var planResult = await GetPlanOrFailAsync(vehicleId, ct);
        if (planResult.IsFailure)
            return planResult.Error;

        var quoteResult = planResult.Value.CalculateQuote(period);
        if (quoteResult.IsFailure)
            return quoteResult.Error;

        return quoteResult.Value.ToDto();
    }

    public async Task<Result<PricingPlanDto>> ConfigurePricingAsync(
        Guid vehicleId,
        ConfigurePricingDto dto,
        CancellationToken ct = default)
    {
        if (vehicleId == Guid.Empty)
            return PricingErrors.InvalidVehicle;

        var authResult = await EnsureVehicleOwnerAsync(vehicleId, dto.OwnerId, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var now = _clock.GetUtcNow();
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        var planResult = await UpsertPlanAsync(vehicleId, dto, now, ct);
        if (planResult.IsFailure)
            return planResult.Error;

        var plan = planResult.Value;
        _dbContext.FeeRules.AddRange(plan.FeeRules);
        _dbContext.PricingPlanVersions.Add(plan.CreateVersionSnapshot(now));
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await PublishIntegrationEventsAsync(plan, ct);

        return ToDto(plan);
    }

    private async Task<Result<PricingPlan>> GetPlanOrFailAsync(Guid vehicleId, CancellationToken ct)
    {
        if (vehicleId == Guid.Empty)
            return PricingErrors.NotFound;

        var plan = await _dbContext.PricingPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.VehicleId == vehicleId, ct);

        return plan is not null ? plan : PricingErrors.NotFound;
    }

    private async Task<Result> EnsureVehicleOwnerAsync(Guid vehicleId, Guid ownerId, CancellationToken ct)
    {
        var vehicleResult = await _fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
        if (vehicleResult.IsFailure)
            return vehicleResult.Error;

        return vehicleResult.Value.OwnerId == ownerId
            ? Result.Success()
            : PricingErrors.OwnerRequired;
    }

    private async Task<Result<PricingPlan>> UpsertPlanAsync(
        Guid vehicleId,
        ConfigurePricingDto dto,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var plan = await _dbContext.PricingPlans
            .IgnoreAutoIncludes()
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
                return createResult.Error;

            plan = createResult.Value;
            _dbContext.PricingPlans.Add(plan);
        }
        else
        {
            await _dbContext.FeeRules
                .Where(rule => rule.PricingPlanId == plan.Id)
                .ExecuteDeleteAsync(ct);

            var updateResult = plan.Update(
                dto.WeekdayRate,
                dto.WeekendRate,
                dto.DepositAmount,
                dto.KmLimit,
                now,
                dto.HourlyRate);
            if (updateResult.IsFailure)
                return updateResult.Error;
        }

        var feeResult = ApplyFeeRules(plan, dto, now);
        if (feeResult.IsFailure)
            return feeResult.Error;

        return plan;
    }

    private static Result ApplyFeeRules(PricingPlan plan, ConfigurePricingDto dto, DateTimeOffset now)
    {
        var overKmResult = ApplyFeeRule(plan, FeeType.OverKm, dto.OverKmFee, now);
        if (overKmResult.IsFailure)
            return overKmResult;

        return ApplyFeeRule(plan, FeeType.Cleaning, dto.CleaningFee, now);
    }

    private static Result ApplyFeeRule(
        PricingPlan plan,
        FeeType type,
        Money? amount,
        DateTimeOffset now) =>
        amount is null
            ? plan.RemoveFeeRule(type, now)
            : plan.SetFeeRule(type, amount, now);

    private async Task PublishIntegrationEventsAsync(PricingPlan plan, CancellationToken ct)
    {
        var integrationEvents = plan.DomainEvents.OfType<IIntegrationEvent>().ToList();
        plan.ClearDomainEvents();
        await _eventOutbox.PublishRangeAsync(integrationEvents, ct);
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
}
