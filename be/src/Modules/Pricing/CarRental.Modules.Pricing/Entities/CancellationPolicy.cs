using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Pricing.Contracts.Dtos;

namespace CarRental.Modules.Pricing.Entities;

public sealed record CancellationTier : ValueObject
{
    public int HoursBeforeStart { get; init; }
    public decimal RefundPercent { get; init; }

    public CancellationTier(int hoursBeforeStart, decimal refundPercent)
    {
        Guard.Against(hoursBeforeStart < 0, "tier.invalid_hours", "Hours before start cannot be negative.");
        Guard.Against(refundPercent < 0 || refundPercent > 100, "tier.invalid_percent", "Refund percent must be between 0 and 100.");

        HoursBeforeStart = hoursBeforeStart;
        RefundPercent = refundPercent;
    }

    private CancellationTier() { }

    public CancellationTierDto ToDto() => new(HoursBeforeStart, RefundPercent);
}

public sealed record CancellationPolicy : ValueObject
{
    private readonly List<CancellationTier> _tiers = [];
    public IReadOnlyList<CancellationTier> Tiers => _tiers.AsReadOnly();

    public CancellationPolicy(IEnumerable<CancellationTier> tiers)
    {
        var tierList = tiers?.OrderByDescending(t => t.HoursBeforeStart).ToList()
            ?? throw new DomainException("policy.empty_tiers", "Cancellation policy must have at least one tier.");

        if (tierList.Count == 0)
        {
            throw new DomainException("policy.empty_tiers", "Cancellation policy must have at least one tier.");
        }

        // Validate monotonic descending refund percent
        for (int i = 0; i < tierList.Count - 1; i++)
        {
            if (tierList[i].RefundPercent < tierList[i + 1].RefundPercent)
            {
                throw new DomainException(
                    "policy.non_monotonic_refund",
                    "Refund percentage must not increase as cancellation approaches pickup time.");
            }
        }

        _tiers = tierList;
    }

    private CancellationPolicy() { }

    public static CancellationPolicy Standard => new(
    [
        new CancellationTier(48, 100m),
        new CancellationTier(24, 50m),
        new CancellationTier(0, 0m)
    ]);

    public static CancellationPolicy Flexible => new(
    [
        new CancellationTier(24, 100m),
        new CancellationTier(0, 50m)
    ]);

    public static CancellationPolicy Strict => new(
    [
        new CancellationTier(72, 100m),
        new CancellationTier(48, 50m),
        new CancellationTier(0, 0m)
    ]);

    public decimal GetRefundPercentage(TimeSpan timeUntilPickup)
    {
        var hours = (int)timeUntilPickup.TotalHours;
        foreach (var tier in _tiers)
        {
            if (hours >= tier.HoursBeforeStart)
            {
                return tier.RefundPercent;
            }
        }

        return 0m;
    }

    public CancellationPolicyDto ToDto() => new(_tiers.Select(t => t.ToDto()).ToList());
}
