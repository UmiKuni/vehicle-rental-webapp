using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Pricing.Contracts.Dtos;

namespace CarRental.Modules.Pricing.Contracts;

public interface IPricingApi
{
    Task<Result<PricingPlanDto>> GetPricingAsync(
        Guid vehicleId,
        CancellationToken ct = default);

    Task<Result<QuoteDto>> GetQuoteAsync(
        Guid vehicleId,
        DateTimeRange period,
        CancellationToken ct = default);

    Task<Result<PricingPlanDto>> ConfigurePricingAsync(
        Guid vehicleId,
        ConfigurePricingDto dto,
        CancellationToken ct = default);
}
