using CarRental.Modules.Availability.Contracts.Dtos;
using CarRental.Modules.Availability.Contracts.Enums;
using CarRental.Modules.Availability.Errors;
using FluentValidation;

namespace CarRental.Modules.Availability.Validators;

internal sealed class BlockScheduleRequestValidator : AbstractValidator<BlockScheduleRequest>
{
    public BlockScheduleRequestValidator()
    {
        RuleFor(x => x.StartTime)
            .NotEmpty()
            .WithErrorCode(AvailabilityErrors.InvalidRange.Code)
            .WithMessage(AvailabilityErrors.InvalidRange.Message);

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .WithErrorCode(AvailabilityErrors.InvalidRange.Code)
            .WithMessage(AvailabilityErrors.InvalidRange.Message)
            .GreaterThan(x => x.StartTime)
            .WithErrorCode(AvailabilityErrors.InvalidRange.Code)
            .WithMessage(AvailabilityErrors.InvalidRange.Message);

        RuleFor(x => x.Type)
            .Must(type => type is null or CalendarEntryType.OwnerBlocked or CalendarEntryType.Maintenance)
            .WithErrorCode(AvailabilityErrors.InvalidBlockType.Code)
            .WithMessage(AvailabilityErrors.InvalidBlockType.Message);
    }
}
