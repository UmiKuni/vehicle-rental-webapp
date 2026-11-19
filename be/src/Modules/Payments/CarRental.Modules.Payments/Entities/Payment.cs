using CarRental.Modules.Payments.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Entities;

internal class Payment : AggregateRoot<PaymentId>
{
    public Guid BookingId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? FailureReason { get; private set; }
    public Money DepositPortion { get; private set; } = null!;
    public Money RefundedAmount { get; private set; } = null!;
    public string? RefundTransactionId { get; private set; }
    public string? RefundReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }

    private Payment() { }

    private Payment(
        PaymentId id,
        Guid bookingId,
        Money amount,
        Money depositPortion,
        DateTimeOffset now)
    {
        Id = id;
        BookingId = bookingId;
        Amount = amount;
        DepositPortion = depositPortion;
        RefundedAmount = Money.Zero(amount.Currency);
        Status = PaymentStatus.Pending;
        Method = PaymentMethod.Transfer;
        CreatedAt = now;
    }

    public static Result<Payment> CreatePending(
        Guid bookingId,
        Money amount,
        Money depositPortion,
        DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure<Payment>(PaymentsErrors.InvalidBooking);

        if (amount == null || amount.Amount <= 0)
            return Result.Failure<Payment>(PaymentsErrors.InvalidAmount);

        if (depositPortion == null || depositPortion.Amount < 0)
            return Result.Failure<Payment>(PaymentsErrors.InvalidDepositPortion);

        if (depositPortion.Amount > amount.Amount)
            return Result.Failure<Payment>(PaymentsErrors.DepositExceedsAmount);

        return Result.Success(new Payment(PaymentId.New(), bookingId, amount, depositPortion, now));
    }

    public Result SetPaymentMethod(PaymentMethod method)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentsErrors.InvalidState("Pending", Status));

        Method = method;
        return Result.Success();
    }

    public Result MarkSuccess(string gatewayTransactionId, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentsErrors.InvalidState("Pending", Status));

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
            return Result.Failure(PaymentsErrors.InvalidTransactionId);

        Status = PaymentStatus.Success;
        GatewayTransactionId = gatewayTransactionId.Trim();
        CompletedAt = now;

        return Result.Success();
    }

    public Result MarkFailed(string gatewayTransactionId, string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentsErrors.InvalidState("Pending", Status));

        Status = PaymentStatus.Failed;
        GatewayTransactionId = string.IsNullOrWhiteSpace(gatewayTransactionId) ? null : gatewayTransactionId.Trim();
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed" : reason.Trim();
        CompletedAt = now;

        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(PaymentsErrors.InvalidState("Pending", Status));

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment cancelled" : reason.Trim();
        CompletedAt = now;

        return Result.Success();
    }

    public Result ValidateRefund(Money refundAmount, string reason)
    {
        if (Status != PaymentStatus.Success && Status != PaymentStatus.PartiallyRefunded)
            return PaymentsErrors.InvalidState("Success or PartiallyRefunded", Status);

        if (string.IsNullOrWhiteSpace(GatewayTransactionId))
            return PaymentsErrors.MissingGatewayTransactionId;

        if (refundAmount is null || refundAmount.Amount <= 0)
            return PaymentsErrors.InvalidRefundAmount;

        if (string.IsNullOrWhiteSpace(reason))
            return PaymentsErrors.RefundReasonRequired;

        var currentRefunded = RefundedAmount?.Amount ?? 0;
        if (currentRefunded + refundAmount.Amount > Amount.Amount)
            return PaymentsErrors.RefundExceedsAmount;

        return Result.Success();
    }

    public Result Refund(Money refundAmount, string refundTransactionId, string reason, DateTimeOffset now)
    {
        var validation = ValidateRefund(refundAmount, reason);
        if (validation.IsFailure)
            return validation;

        if (string.IsNullOrWhiteSpace(refundTransactionId))
            return PaymentsErrors.InvalidTransactionId;

        var currentRefunded = RefundedAmount?.Amount ?? 0;
        var totalRefunded = currentRefunded + refundAmount.Amount;
        RefundedAmount = Money.Of(totalRefunded, Amount.Currency);
        RefundTransactionId = refundTransactionId.Trim();
        RefundReason = reason.Trim();
        RefundedAt = now;
        Status = totalRefunded == Amount.Amount
            ? PaymentStatus.Refunded
            : PaymentStatus.PartiallyRefunded;

        return Result.Success();
    }
}
