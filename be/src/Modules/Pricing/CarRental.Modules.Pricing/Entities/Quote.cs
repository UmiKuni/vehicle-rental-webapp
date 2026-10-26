using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Pricing.Contracts.Dtos;

namespace CarRental.Modules.Pricing.Entities;

public sealed record Quote : ValueObject
{
    public Guid VehicleId { get; init; }
    public Money Total { get; init; } = Money.Zero();
    public Money DepositAmount { get; init; } = Money.Zero();
    public int KmLimitPerDay { get; init; }
    public Money? OverKmFeePerKm { get; init; }
    public IReadOnlyList<QuoteLine> Lines { get; init; } = [];
    public CancellationPolicy? Policy { get; init; }

    public Quote(
        Guid vehicleId,
        Money total,
        Money depositAmount,
        int kmLimitPerDay,
        Money? overKmFeePerKm,
        IReadOnlyList<QuoteLine> lines,
        CancellationPolicy? policy)
    {
        VehicleId = vehicleId;
        Total = total;
        DepositAmount = depositAmount;
        KmLimitPerDay = kmLimitPerDay;
        OverKmFeePerKm = overKmFeePerKm;
        Lines = lines ?? [];
        Policy = policy;
    }

    private Quote() { }

    public QuoteDto ToDto() => new(
        VehicleId,
        Total,
        DepositAmount,
        KmLimitPerDay,
        OverKmFeePerKm,
        Lines.Select(l => l.ToDto()).ToList(),
        Policy?.ToDto());
}
