using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Bookings.Errors;
using FluentValidation;

namespace CarRental.Modules.Bookings.Validators;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.VehicleId)
            .NotEmpty()
            .WithErrorCode(BookingsErrors.InvalidVehicle.Code)
            .WithMessage(BookingsErrors.InvalidVehicle.Message);

        RuleFor(x => x.StartTime)
            .NotEmpty()
            .WithErrorCode(BookingsErrors.InvalidPeriod.Code)
            .WithMessage(BookingsErrors.InvalidPeriod.Message);

        RuleFor(x => x.EndTime)
            .NotEmpty()
            .WithErrorCode(BookingsErrors.InvalidPeriod.Code)
            .WithMessage(BookingsErrors.InvalidPeriod.Message)
            .GreaterThan(x => x.StartTime)
            .When(x => x.StartTime != default && x.EndTime != default)
            .WithErrorCode(BookingsErrors.InvalidDuration.Code)
            .WithMessage(BookingsErrors.InvalidDuration.Message);

        RuleFor(x => x.PickupLocation)
            .Must(loc => !string.IsNullOrWhiteSpace(loc))
            .WithErrorCode(BookingsErrors.InvalidLocation.Code)
            .WithMessage(BookingsErrors.InvalidLocation.Message);
    }
}

public sealed class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequest>
{
    public RejectBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithErrorCode(BookingsErrors.ReasonRequired.Code)
            .WithMessage(BookingsErrors.ReasonRequired.Message);
    }
}

public sealed class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithErrorCode(BookingsErrors.ReasonRequired.Code)
            .WithMessage(BookingsErrors.ReasonRequired.Message);
    }
}

public sealed class AdminCancelBookingRequestValidator : AbstractValidator<AdminCancelBookingRequest>
{
    public AdminCancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithErrorCode(BookingsErrors.ReasonRequired.Code)
            .WithMessage(BookingsErrors.ReasonRequired.Message);
    }
}
