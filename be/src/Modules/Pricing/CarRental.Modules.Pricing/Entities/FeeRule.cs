using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Pricing.Contracts.Enums;

namespace CarRental.Modules.Pricing.Entities;

public class FeeRule : Entity<Guid>
{
    public Guid PricingPlanId { get; private set; }
    public FeeType Type { get; private set; }
    public Money Amount { get; private set; } = Money.Zero();

    private FeeRule() { }

    internal FeeRule(Guid id, Guid pricingPlanId, FeeType type, Money amount)
    {
        Id = id;
        PricingPlanId = pricingPlanId;
        Type = type;
        Amount = amount;
    }

    internal void UpdateAmount(Money amount)
    {
        Amount = amount;
    }
}
