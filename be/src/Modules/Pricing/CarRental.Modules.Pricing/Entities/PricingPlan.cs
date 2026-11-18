using CarRental.Modules.Pricing.Errors;
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
            return PricingErrors.InvalidVehicle;

        var validation = ValidateRatesAndLimits(weekdayRate, weekendRate, depositAmount, kmLimit, hourlyRate);
        if (validation.IsFailure)
            return validation.Error;

        var plan = new PricingPlan(Guid.CreateVersion7(), vehicleId, hourlyRate, weekdayRate, weekendRate, depositAmount, kmLimit);
        plan.RaisePricingUpdated(now);

        return plan;
    }

    public Result Update(
        Money weekdayRate,
        Money weekendRate,
        Money depositAmount,
        int kmLimit,
        DateTimeOffset now,
        Money? hourlyRate = null)
    {
        var validation = ValidateRatesAndLimits(weekdayRate, weekendRate, depositAmount, kmLimit, hourlyRate);
        if (validation.IsFailure)
            return validation;

        HourlyRate = hourlyRate;
        WeekdayRate = weekdayRate;
        WeekendRate = weekendRate;
        DepositAmount = depositAmount;
        KmLimit = kmLimit;

        RaisePricingUpdated(now);
        return Result.Success();
    }

    public Result SetFeeRule(FeeType type, Money amount, DateTimeOffset now)
    {
        if (amount is null || amount.Amount < 0)
            return PricingErrors.InvalidFeeAmount;

        var existing = _feeRules.FirstOrDefault(f => f.Type == type);
        if (existing is null)
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
        if (period is null)
            return PricingErrors.InvalidPeriod;

        var (baseDescription, baseAmount, currency) = HourlyRate is not null
            ? CalculateHourlyBase(period)
            : CalculateDailyBase(period);

        return BuildQuote(baseDescription, baseAmount, currency);
    }

    private static Result ValidateRatesAndLimits(
        Money weekdayRate,
        Money weekendRate,
        Money depositAmount,
        int kmLimit,
        Money? hourlyRate)
    {
        if (weekdayRate is null || weekdayRate.Amount <= 0)
            return PricingErrors.InvalidWeekdayRate;

        if (weekendRate is null || weekendRate.Amount <= 0)
            return PricingErrors.InvalidWeekendRate;

        if (hourlyRate is not null && hourlyRate.Amount <= 0)
            return PricingErrors.InvalidHourlyRate;

        if (depositAmount is null || depositAmount.Amount < 0)
            return PricingErrors.InvalidDeposit;

        if (kmLimit <= 0)
            return PricingErrors.InvalidKmLimit;

        return Result.Success();
    }

    private void RaisePricingUpdated(DateTimeOffset now)
    {
        var minPrice = Math.Min(WeekdayRate.Amount, WeekendRate.Amount);
        Raise(new PricingUpdated(Guid.CreateVersion7(), now, VehicleId, minPrice));
    }

    private (string Description, long Amount, string Currency) CalculateHourlyBase(DateTimeRange period)
    {
        var totalHours = Math.Max(1, (int)Math.Ceiling(period.Duration.TotalHours));
        var hourlyTotal = checked(HourlyRate!.Amount * totalHours);
        return ($"Rent ({totalHours} hours)", hourlyTotal, HourlyRate.Currency);
    }

    private (string Description, long Amount, string Currency) CalculateDailyBase(DateTimeRange period)
    {
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
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
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

        return ($"Rent ({weekdayCount} weekdays, {weekendCount} weekend days)", totalRent, WeekdayRate.Currency);
    }

    private Quote BuildQuote(string baseDescription, long baseAmount, string currency)
    {
        var lines = new List<QuoteLine>
        {
            new(QuoteLineType.BaseRate, baseDescription, new Money(baseAmount, currency))
        };

        var subtotal = baseAmount;
        var cleaningRule = _feeRules.FirstOrDefault(f => f.Type == FeeType.Cleaning);
        if (cleaningRule is not null && cleaningRule.Amount.Amount > 0)
        {
            lines.Add(new QuoteLine(QuoteLineType.CleaningFee, "Cleaning fee", cleaningRule.Amount));
            subtotal = checked(subtotal + cleaningRule.Amount.Amount);
        }

        lines.Add(new QuoteLine(QuoteLineType.Deposit, "Security deposit", DepositAmount));

        var overKmFee = _feeRules.FirstOrDefault(f => f.Type == FeeType.OverKm)?.Amount;

        return new Quote(
            VehicleId,
            new Money(subtotal, currency),
            DepositAmount,
            KmLimit,
            overKmFee,
            lines,
            CancellationPolicy.Standard);
    }
}
