using CarRental.Modules.Handover.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Errors;
using FluentValidation;

namespace CarRental.Modules.Handover.Validators;

internal sealed class CreateHandoverRequestValidator : AbstractValidator<CreateHandoverRequest>
{
    public CreateHandoverRequestValidator()
    {
        RuleFor(x => x.BookingId)
            .NotEmpty()
            .WithErrorCode(HandoverErrors.InvalidContext.Code)
            .WithMessage(HandoverErrors.InvalidContext.Message);

        RuleFor(x => x.Type)
            .Must(HandoverValidationRules.BeValidEnum<HandoverType>)
            .WithErrorCode(HandoverErrors.InvalidType.Code)
            .WithMessage(HandoverErrors.InvalidType.Message);
    }
}

internal sealed class UpdateVehicleConditionRequestValidator : AbstractValidator<UpdateVehicleConditionRequest>
{
    public UpdateVehicleConditionRequestValidator()
    {
        RuleFor(x => x.Odo)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode(HandoverErrors.InvalidOdometer.Code)
            .WithMessage(HandoverErrors.InvalidOdometer.Message);

        RuleFor(x => x.FuelLevel)
            .InclusiveBetween(0, 100)
            .WithErrorCode(HandoverErrors.InvalidFuel.Code)
            .WithMessage(HandoverErrors.InvalidFuel.Message);
    }
}

internal sealed class UploadHandoverPhotoRequestValidator : AbstractValidator<UploadHandoverPhotoRequest>
{
    public UploadHandoverPhotoRequestValidator()
    {
        RuleFor(x => x.Corner)
            .Must(HandoverValidationRules.BeValidEnum<PhotoCorner>)
            .WithErrorCode(HandoverErrors.InvalidCorner.Code)
            .WithMessage(HandoverErrors.InvalidCorner.Message);

        RuleFor(x => x.StorageKey)
            .Must(k => !string.IsNullOrWhiteSpace(k))
            .WithErrorCode(HandoverErrors.InvalidPhoto.Code)
            .WithMessage(HandoverErrors.InvalidPhoto.Message);

        RuleFor(x => x.FileHash)
            .Must(h => !string.IsNullOrWhiteSpace(h))
            .WithErrorCode(HandoverErrors.MissingHash.Code)
            .WithMessage(HandoverErrors.MissingHash.Message);
    }
}

internal sealed class ConfirmHandoverRequestValidator : AbstractValidator<ConfirmHandoverRequest>
{
    public ConfirmHandoverRequestValidator()
    {
        RuleFor(x => x.ConfirmedByRole)
            .Must(BeValidConfirmationRole)
            .WithErrorCode(HandoverErrors.InvalidRole.Code)
            .WithMessage(HandoverErrors.InvalidRole.Message);
    }

    private static bool BeValidConfirmationRole(string? role) =>
        !string.IsNullOrWhiteSpace(role) &&
        (string.Equals(role.Trim(), "Customer", StringComparison.OrdinalIgnoreCase)
         || string.Equals(role.Trim(), "Owner", StringComparison.OrdinalIgnoreCase));
}

internal static class HandoverValidationRules
{
    public static bool BeValidEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value)
        && !int.TryParse(value, out _)
        && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);
}
