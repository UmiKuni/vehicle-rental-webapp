using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Pricing.Contracts.Dtos;

public sealed record QuoteLineDto(
    string Type,
    string Description,
    Money Amount);

public sealed record CancellationTierDto(
    int HoursBeforeStart,
    decimal RefundPercent);

public sealed record CancellationPolicyDto(
    IReadOnlyList<CancellationTierDto> Tiers);

public sealed record QuoteDto(
    Guid VehicleId,
    Money Total,
    Money DepositAmount,
    int KmLimitPerDay,
    Money? OverKmFeePerKm,
    IReadOnlyList<QuoteLineDto> Lines,
    CancellationPolicyDto? Policy);

public sealed record PricingPlanDto(
    Guid VehicleId,
    Money? HourlyRate,
    Money WeekdayRate,
    Money WeekendRate,
    Money DepositAmount,
    int KmLimit,
    IReadOnlyList<FeeRuleDto> FeeRules);

public sealed record FeeRuleDto(
    string Type,
    Money Amount);

public sealed record ConfigurePricingDto(
    Money? HourlyRate,
    Money WeekdayRate,
    Money WeekendRate,
    Money DepositAmount,
    int KmLimit,
    Money? OverKmFee,
    Money? CleaningFee,
    Guid OwnerId);

public sealed record PricingQuoteRequest(
    Guid VehicleId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime);
