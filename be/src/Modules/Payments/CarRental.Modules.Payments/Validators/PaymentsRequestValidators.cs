using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Errors;
using FluentValidation;

namespace CarRental.Modules.Payments.Validators;

public sealed class InitiatePaymentRequestValidator : AbstractValidator<InitiatePaymentRequest>
{
    public InitiatePaymentRequestValidator()
    {
        RuleFor(x => x.BookingId)
            .NotEmpty()
            .WithErrorCode(PaymentsErrors.InvalidBooking.Code)
            .WithMessage(PaymentsErrors.InvalidBooking.Message);

        RuleFor(x => x.Method)
            .Must(BeValidPaymentMethod)
            .WithErrorCode(PaymentsErrors.InvalidMethod.Code)
            .WithMessage(PaymentsErrors.InvalidMethod.Message);

        RuleFor(x => x.ReturnUrl)
            .Must(BeValidReturnUrl)
            .WithErrorCode(PaymentsErrors.InvalidReturnUrl.Code)
            .WithMessage(PaymentsErrors.InvalidReturnUrl.Message);
    }

    private static bool BeValidPaymentMethod(string? method) =>
        !string.IsNullOrWhiteSpace(method)
        && !int.TryParse(method, out _)
        && Enum.TryParse<PaymentMethod>(method.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed);

    private static bool BeValidReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Uri.TryCreate(returnUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public sealed class RefundPaymentRequestValidator : AbstractValidator<RefundPaymentRequest>
{
    public RefundPaymentRequestValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithErrorCode(PaymentsErrors.InvalidRefundAmount.Code)
            .WithMessage(PaymentsErrors.InvalidRefundAmount.Message);

        RuleFor(x => x.Reason)
            .Must(r => !string.IsNullOrWhiteSpace(r))
            .WithErrorCode(PaymentsErrors.RefundReasonRequired.Code)
            .WithMessage(PaymentsErrors.RefundReasonRequired.Message);
    }
}

public sealed class DeductDepositRequestValidator : AbstractValidator<DeductDepositRequest>
{
    public DeductDepositRequestValidator()
    {
        RuleFor(x => x.DeductAmount)
            .GreaterThan(0m)
            .WithErrorCode(PaymentsErrors.InvalidDeductAmount.Code)
            .WithMessage(PaymentsErrors.InvalidDeductAmount.Message);
    }
}

public sealed class CapturePayPalOrderRequestValidator : AbstractValidator<CapturePayPalOrderRequest>
{
    public CapturePayPalOrderRequestValidator()
    {
        RuleFor(x => x.OrderId)
            .Must(id => !string.IsNullOrWhiteSpace(id))
            .WithErrorCode(PaymentsErrors.InvalidPayPalOrderId.Code)
            .WithMessage(PaymentsErrors.InvalidPayPalOrderId.Message);
    }
}
