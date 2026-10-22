using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Pricing.Contracts.Enums;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Pricing.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class PricingModuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_ShouldReturnPlanAndRaiseEvent()
    {
        var vehicleId = Guid.NewGuid();
        var weekdayRate = new Money(500_000, "VND");
        var weekendRate = new Money(700_000, "VND");
        var deposit = new Money(5_000_000, "VND");

        var result = PricingPlan.Create(vehicleId, weekdayRate, weekendRate, deposit, 300, Now);

        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;
        plan.VehicleId.Should().Be(vehicleId);
        plan.WeekdayRate.Amount.Should().Be(500_000);
        plan.WeekendRate.Amount.Should().Be(700_000);
        plan.DepositAmount.Amount.Should().Be(5_000_000);
        plan.KmLimit.Should().Be(300);
        plan.DomainEvents.Should().ContainSingle(e => e is PricingUpdated);
    }

    [Fact]
    public void Create_WithInvalidParameters_ShouldFail()
    {
        var validRate = new Money(500_000, "VND");
        var validDeposit = new Money(5_000_000, "VND");

        var emptyVehicle = PricingPlan.Create(Guid.Empty, validRate, validRate, validDeposit, 300, Now);
        emptyVehicle.IsSuccess.Should().BeFalse();
        emptyVehicle.Error.Code.Should().Be("pricing.invalid_vehicle");

        var zeroKmLimit = PricingPlan.Create(Guid.NewGuid(), validRate, validRate, validDeposit, 0, Now);
        zeroKmLimit.IsSuccess.Should().BeFalse();
        zeroKmLimit.Error.Code.Should().Be("pricing.invalid_km_limit");
    }

    [Fact]
    public void SetFeeRule_And_RemoveFeeRule_ShouldManageRulesCorrectly()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(),
            new Money(500_000, "VND"),
            new Money(700_000, "VND"),
            new Money(5_000_000, "VND"),
            300,
            Now).Value;

        var addOverKm = plan.SetFeeRule(FeeType.OverKm, new Money(5_000, "VND"), Now);
        addOverKm.IsSuccess.Should().BeTrue();
        plan.FeeRules.Should().HaveCount(1);
        plan.FeeRules.First().Type.Should().Be(FeeType.OverKm);
        plan.FeeRules.First().Amount.Amount.Should().Be(5_000);

        var addCleaning = plan.SetFeeRule(FeeType.Cleaning, new Money(100_000, "VND"), Now);
        addCleaning.IsSuccess.Should().BeTrue();
        plan.FeeRules.Should().HaveCount(2);

        // Upsert existing fee rule
        plan.SetFeeRule(FeeType.OverKm, new Money(6_000, "VND"), Now);
        plan.FeeRules.Should().HaveCount(2);
        plan.FeeRules.First(f => f.Type == FeeType.OverKm).Amount.Amount.Should().Be(6_000);

        // Remove
        plan.RemoveFeeRule(FeeType.Cleaning, Now);
        plan.FeeRules.Should().HaveCount(1);
        plan.FeeRules.Any(f => f.Type == FeeType.Cleaning).Should().BeFalse();
    }

    [Fact]
    public void CalculateQuote_ShouldComputeWeekdayAndWeekendRatesCorrectly()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(),
            new Money(1_000_000, "VND"), // 1M / weekday
            new Money(1_500_000, "VND"), // 1.5M / weekend
            new Money(5_000_000, "VND"),
            300,
            Now).Value;

        plan.SetFeeRule(FeeType.Cleaning, new Money(100_000, "VND"), Now);
        plan.SetFeeRule(FeeType.OverKm, new Money(5_000, "VND"), Now);

        // Rental: Monday 2026-10-26 09:00 UTC (16:00 UTC+7) to Sunday 2026-11-01 11:00 UTC (18:00 UTC+7)
        // 5 weekdays (Mon-Fri) @ 1,000,000 = 5,000,000
        // 2 weekend days (Sat-Sun) @ 1,500,000 = 3,000,000
        // Total rent = 8,000,000
        var start = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero);
        var period = DateTimeRange.Create(start, end).Value;

        var quoteResult = plan.CalculateQuote(period);
        quoteResult.IsSuccess.Should().BeTrue();

        var quote = quoteResult.Value;
        // Total = 8,000,000 + 100,000 (cleaning) = 8,100,000
        quote.Total.Amount.Should().Be(8_100_000);
        quote.DepositAmount.Amount.Should().Be(5_000_000);
        quote.KmLimitPerDay.Should().Be(300);
        quote.OverKmFeePerKm!.Amount.Should().Be(5_000);
        quote.Lines.Should().Contain(l => l.Type == QuoteLineType.BaseRate && l.Amount.Amount == 8_000_000);
        quote.Lines.Should().Contain(l => l.Type == QuoteLineType.CleaningFee && l.Amount.Amount == 100_000);
        quote.Lines.Should().Contain(l => l.Type == QuoteLineType.Deposit && l.Amount.Amount == 5_000_000);
    }

    [Fact]
    public void CreateVersionSnapshot_ShouldProduceValidAuditRecord()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(),
            new Money(500_000, "VND"),
            new Money(700_000, "VND"),
            new Money(5_000_000, "VND"),
            300,
            Now).Value;

        var snapshot = plan.CreateVersionSnapshot(Now);

        snapshot.Should().NotBeNull();
        snapshot.VehicleId.Should().Be(plan.VehicleId);
        snapshot.Config.Should().Contain("500000");
        snapshot.Config.Should().Contain("700000");
        snapshot.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void CancellationPolicy_TiersNotMonotonic_ShouldThrowDomainException()
    {
        var invalidTiers = new List<CancellationTier>
        {
            new(48, 50m),
            new(24, 100m)
        };

        var action = () => new CancellationPolicy(invalidTiers);

        action.Should().Throw<DomainException>()
            .Which.Code.Should().Be("policy.non_monotonic_refund");
    }
}
