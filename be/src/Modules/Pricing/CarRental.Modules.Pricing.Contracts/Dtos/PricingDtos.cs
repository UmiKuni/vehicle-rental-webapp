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
