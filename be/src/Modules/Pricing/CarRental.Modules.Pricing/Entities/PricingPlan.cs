using System.Text.Json;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Pricing.Contracts.Enums;
using CarRental.Modules.Pricing.Contracts.Events;

namespace CarRental.Modules.Pricing.Entities;

public class PricingPlan : AggregateRoot<Guid>
{
    private readonly List<FeeRule> _feeRules = new();

    public Guid VehicleId { get; private set; }
    public Money? HourlyRate { get; private set; }
    public Money WeekdayRate { get; private set; } = Money.Zero();
    public Money WeekendRate { get; private set; } = Money.Zero();
    public Money DepositAmount { get; private set; } = Money.Zero();
    public int KmLimit { get; private set; }

    public IReadOnlyCollection<FeeRule> FeeRules => _feeRules.AsReadOnly();

    private PricingPlan() { }

    private PricingPlan(
        Guid id,
        Guid vehicleId,
        Money? hourlyRate,
        Money weekdayRate,
        Money weekendRate,
        Money depositAmount,
        int kmLimit)
    {
        Id = id;
        VehicleId = vehicleId;
        HourlyRate = hourlyRate;
        WeekdayRate = weekdayRate;
        WeekendRate = weekendRate;
        DepositAmount = depositAmount;
        KmLimit = kmLimit;
    }

    public static Result<PricingPlan> Create(
        Guid vehicleId,
        Money weekdayRate,
        Money weekendRate,
        Money depositAmount,
        int kmLimit,
        DateTimeOffset now,
        Money? hourlyRate = null)
    {
        if (vehicleId == Guid.Empty)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_vehicle", "Vehicle ID is required."));

        if (weekdayRate == null || weekdayRate.Amount <= 0)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_weekday_rate", "Weekday rate must be greater than zero."));

        if (weekendRate == null || weekendRate.Amount <= 0)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_weekend_rate", "Weekend rate must be greater than zero."));

        if (hourlyRate is not null && hourlyRate.Amount <= 0)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_hourly_rate", "Hourly rate must be greater than zero."));

        if (depositAmount == null || depositAmount.Amount < 0)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_deposit", "Deposit amount cannot be negative."));

        if (kmLimit <= 0)
            return Result.Failure<PricingPlan>(new Error("pricing.invalid_km_limit", "Daily KM limit must be greater than zero."));

        var plan = new PricingPlan(Guid.CreateVersion7(), vehicleId, hourlyRate, weekdayRate, weekendRate, depositAmount, kmLimit);

        var minPrice = Math.Min(weekdayRate.Amount, weekendRate.Amount);
        plan.Raise(new PricingUpdated(Guid.CreateVersion7(), now, vehicleId, minPrice));

        return Result.Success(plan);
    }

    public Result Update(
        Money weekdayRate,
        Money weekendRate,
        Money depositAmount,
        int kmLimit,
        DateTimeOffset now,
        Money? hourlyRate = null)
    {
        if (weekdayRate == null || weekdayRate.Amount <= 0)
            return Result.Failure(new Error("pricing.invalid_weekday_rate", "Weekday rate must be greater than zero."));

        if (weekendRate == null || weekendRate.Amount <= 0)
            return Result.Failure(new Error("pricing.invalid_weekend_rate", "Weekend rate must be greater than zero."));

        if (hourlyRate is not null && hourlyRate.Amount <= 0)
            return Result.Failure(new Error("pricing.invalid_hourly_rate", "Hourly rate must be greater than zero."));

        if (depositAmount == null || depositAmount.Amount < 0)
            return Result.Failure(new Error("pricing.invalid_deposit", "Deposit amount cannot be negative."));

        if (kmLimit <= 0)
            return Result.Failure(new Error("pricing.invalid_km_limit", "Daily KM limit must be greater than zero."));

        HourlyRate = hourlyRate;
        WeekdayRate = weekdayRate;
        WeekendRate = weekendRate;
        DepositAmount = depositAmount;
        KmLimit = kmLimit;

        var minPrice = Math.Min(weekdayRate.Amount, weekendRate.Amount);
        Raise(new PricingUpdated(Guid.CreateVersion7(), now, VehicleId, minPrice));

        return Result.Success();
    }

    public Result SetFeeRule(FeeType type, Money amount, DateTimeOffset now)
    {
        if (amount == null || amount.Amount < 0)
            return Result.Failure(new Error("pricing.invalid_fee_amount", "Fee amount cannot be negative."));

        var existing = _feeRules.FirstOrDefault(f => f.Type == type);
        if (existing == null)
        {
            _feeRules.Add(new FeeRule(Guid.CreateVersion7(), Id, type, amount));
        }
        else
        {
            existing.UpdateAmount(amount);
        }

        return Result.Success();
    }

    public Result RemoveFeeRule(FeeType type, DateTimeOffset now)
    {
        _feeRules.RemoveAll(f => f.Type == type);
        return Result.Success();
    }

    public PricingPlanVersion CreateVersionSnapshot(DateTimeOffset now)
    {
        var configObj = new
        {
            VehicleId,
            HourlyRate = HourlyRate?.Amount,
            WeekdayRate = WeekdayRate.Amount,
            WeekendRate = WeekendRate.Amount,
            DepositAmount = DepositAmount.Amount,
            KmLimit,
            FeeRules = _feeRules.Select(f => new { Type = f.Type.ToString(), Amount = f.Amount.Amount })
        };
        var json = JsonSerializer.Serialize(configObj);
        return new PricingPlanVersion(Guid.CreateVersion7(), VehicleId, json, now);
    }

    public Result<Quote> CalculateQuote(DateTimeRange period)
    {
        if (period == null)
            return Result.Failure<Quote>(new Error("pricing.invalid_period", "Rental period is required."));

        var lines = new List<QuoteLine>();
        var currency = WeekdayRate.Currency;

        if (HourlyRate is not null)
        {
            var totalHours = Math.Max(1, (int)Math.Ceiling(period.Duration.TotalHours));
            var hourlyTotal = checked(HourlyRate.Amount * totalHours);
            lines.Add(new QuoteLine(
                QuoteLineType.BaseRate,
                $"Rent ({totalHours} hours)",
                new Money(hourlyTotal, HourlyRate.Currency)));
            var hourlyCleaningRule = _feeRules.FirstOrDefault(f => f.Type == FeeType.Cleaning);
            if (hourlyCleaningRule is not null && hourlyCleaningRule.Amount.Amount > 0)
            {
                lines.Add(new QuoteLine(QuoteLineType.CleaningFee, "Cleaning fee", hourlyCleaningRule.Amount));
                hourlyTotal = checked(hourlyTotal + hourlyCleaningRule.Amount.Amount);
            }
            lines.Add(new QuoteLine(QuoteLineType.Deposit, "Security deposit", DepositAmount));

            Money? hourlyOverKmFee = _feeRules.FirstOrDefault(f => f.Type == FeeType.OverKm)?.Amount;
            return Result.Success(new Quote(
                VehicleId,
                new Money(hourlyTotal, HourlyRate.Currency),
                DepositAmount,
                KmLimit,
                hourlyOverKmFee,
                lines,
                CancellationPolicy.Standard));
        }

        // Calculate days in UTC+7 (Vietnam Time)
        var vnOffset = TimeSpan.FromHours(7);
        var startVn = period.Start.ToOffset(vnOffset);
        var endVn = period.End.ToOffset(vnOffset);

        long totalRent = 0;
        var currentDate = startVn.Date;
        var endDate = endVn.Date;
        if (endVn > startVn && endVn.TimeOfDay == TimeSpan.Zero)
        {
            endDate = endDate.AddDays(-1);
        }

        int weekdayCount = 0;
        int weekendCount = 0;

        for (var d = currentDate; d <= endDate; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
            {
                weekendCount++;
                totalRent += WeekendRate.Amount;
            }
            else
            {
                weekdayCount++;
                totalRent += WeekdayRate.Amount;
            }
        }

        if (weekdayCount == 0 && weekendCount == 0)
        {
            weekdayCount = 1;
            totalRent = WeekdayRate.Amount;
        }

        lines.Add(new QuoteLine(QuoteLineType.BaseRate, $"Rent ({weekdayCount} weekdays, {weekendCount} weekend days)", new Money(totalRent, currency)));

        long subtotal = totalRent;

        var cleaningRule = _feeRules.FirstOrDefault(f => f.Type == FeeType.Cleaning);
        if (cleaningRule != null && cleaningRule.Amount.Amount > 0)
        {
            lines.Add(new QuoteLine(QuoteLineType.CleaningFee, "Cleaning fee", cleaningRule.Amount));
            subtotal += cleaningRule.Amount.Amount;
        }

        lines.Add(new QuoteLine(QuoteLineType.Deposit, "Security deposit", DepositAmount));

        Money? overKmFee = _feeRules.FirstOrDefault(f => f.Type == FeeType.OverKm)?.Amount;

        var quote = new Quote(
            VehicleId,
            new Money(subtotal, currency),
            DepositAmount,
            KmLimit,
            overKmFee,
            lines,
            CancellationPolicy.Standard);

        return Result.Success(quote);
    }
}
