using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Pricing.Errors;
using FluentValidation;

namespace CarRental.Modules.Pricing.Validators;

public sealed class PricingQuoteRequestValidator : AbstractValidator<PricingQuoteRequest>
{
    public PricingQuoteRequestValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithErrorCode(PricingErrors.InvalidVehicle.Code)
            .WithMessage(PricingErrors.InvalidVehicle.Message);

        RuleFor(x => x.StartTime)
            .NotEmpty()
            .WithErrorCode(PricingErrors.InvalidPeriod.Code)
            .WithMessage(PricingErrors.InvalidPeriod.Message);

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .WithErrorCode(PricingErrors.InvalidPeriod.Code)
            .WithMessage(PricingErrors.InvalidPeriod.Message)
            .GreaterThan(x => x.StartTime)
            .WithErrorCode("datetime_range.invalid_range")
            .WithMessage("End date must be strictly after start date.");
    }
}

public sealed class ConfigurePricingDtoValidator : AbstractValidator<ConfigurePricingDto>
{
    public ConfigurePricingDtoValidator()
    {
        RuleFor(x => x.WeekdayRate)
            .Must(rate => rate is not null && rate.Amount > 0)
            .WithErrorCode(PricingErrors.InvalidWeekdayRate.Code)
            .WithMessage(PricingErrors.InvalidWeekdayRate.Message);

        RuleFor(x => x.WeekendRate)
            .Must(rate => rate is not null && rate.Amount > 0)
            .WithErrorCode(PricingErrors.InvalidWeekendRate.Code)
            .WithMessage(PricingErrors.InvalidWeekendRate.Message);

        RuleFor(x => x.HourlyRate)
            .Must(rate => rate is null || rate.Amount > 0)
            .WithErrorCode(PricingErrors.InvalidHourlyRate.Code)
            .WithMessage(PricingErrors.InvalidHourlyRate.Message);

        RuleFor(x => x.DepositAmount)
            .Must(deposit => deposit is not null && deposit.Amount >= 0)
            .WithErrorCode(PricingErrors.InvalidDeposit.Code)
            .WithMessage(PricingErrors.InvalidDeposit.Message);

        RuleFor(x => x.KmLimit)
            .GreaterThan(0)
            .WithErrorCode(PricingErrors.InvalidKmLimit.Code)
            .WithMessage(PricingErrors.InvalidKmLimit.Message);

        RuleFor(x => x.OverKmFee)
            .Must(fee => fee is null || fee.Amount >= 0)
            .WithErrorCode(PricingErrors.InvalidFeeAmount.Code)
            .WithMessage(PricingErrors.InvalidFeeAmount.Message);

        RuleFor(x => x.CleaningFee)
            .Must(fee => fee is null || fee.Amount >= 0)
            .WithErrorCode(PricingErrors.InvalidFeeAmount.Code)
            .WithMessage(PricingErrors.InvalidFeeAmount.Message);
    }
}
