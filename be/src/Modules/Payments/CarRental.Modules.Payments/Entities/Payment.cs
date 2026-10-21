using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Entities;

public class Payment : AggregateRoot<PaymentId>
{
    public Guid BookingId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public PaymentStatus Status { get; private set; }
    public string? GatewayTransactionId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? FailureReason { get; private set; }
    public Money DepositPortion { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

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
            return Result.Failure<Payment>(new Error("payment.invalid_booking", "Booking ID is required."));

        if (amount == null || amount.Amount <= 0)
            return Result.Failure<Payment>(new Error("payment.invalid_amount", "Payment amount must be greater than zero."));

        if (depositPortion == null || depositPortion.Amount < 0)
            return Result.Failure<Payment>(new Error("payment.invalid_deposit_portion", "Deposit portion cannot be negative."));

        if (depositPortion.Amount > amount.Amount)
            return Result.Failure<Payment>(new Error("payment.deposit_exceeds_amount", "Deposit portion cannot exceed total payment amount."));

        return Result.Success(new Payment(PaymentId.New(), bookingId, amount, depositPortion, now));
    }

    public Result SetPaymentMethod(PaymentMethod method)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(new Error("payment.invalid_state", "Payment method can only be updated for pending payments."));

        Method = method;
        return Result.Success();
    }

    public Result MarkSuccess(string gatewayTransactionId, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(new Error("payment.invalid_state", $"Only pending payments can be marked success. Current status: {Status}."));

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
            return Result.Failure(new Error("payment.invalid_transaction_id", "Gateway transaction ID is required."));

        Status = PaymentStatus.Success;
        GatewayTransactionId = gatewayTransactionId.Trim();
        CompletedAt = now;

        return Result.Success();
    }

    public Result MarkFailed(string gatewayTransactionId, string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(new Error("payment.invalid_state", $"Only pending payments can be marked failed. Current status: {Status}."));

        Status = PaymentStatus.Failed;
        GatewayTransactionId = string.IsNullOrWhiteSpace(gatewayTransactionId) ? null : gatewayTransactionId.Trim();
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment failed" : reason.Trim();
        CompletedAt = now;

        return Result.Success();
    }

    public Result Cancel(string reason, DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
            return Result.Failure(new Error("payment.invalid_state", $"Only pending payments can be cancelled. Current status: {Status}."));

        Status = PaymentStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Payment cancelled" : reason.Trim();
        CompletedAt = now;

        return Result.Success();
    }
}
