using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Errors;

internal static class PaymentsErrors
{
    public static Error DepositInvalidBooking => Error.Validation("deposit.invalid_booking", "Booking ID is required.");

    public static Error DepositInvalidAmount => Error.Validation("deposit.invalid_amount", "Deposit amount cannot be negative.");

    public static Error Frozen => Error.Conflict("deposit.frozen", "Cannot release a frozen deposit.");

    public static Error InvalidDeduction => Error.Validation("deposit.invalid_deduction", "Deduction amount must be greater than zero.");

    public static Error DeductionExceedsAmount => Error.Validation("deposit.deduction_exceeds_amount", "Deduction amount cannot exceed total deposit amount.");

    public static Error InvalidPercent => Error.Validation("deposit.invalid_percent", "Customer refund percentage must be between 0 and 100.");

    public static Error DepositInvalidState(string expected, DepositStatus current) =>
        Error.Conflict("deposit.invalid_state", $"Deposit must be in {expected} state. Current status: {current}.");

    public static Error DepositNotFound => Error.NotFound("deposit.not_found", "Security deposit was not found.");

    public static Error InvalidDeductAmount => Error.Validation("deposit.invalid_deduct_amount", "Deduction amount must be greater than zero.");

    public static Error InvalidBooking => Error.Validation("payment.invalid_booking", "Booking ID is required.");

    public static Error InvalidAmount => Error.Validation("payment.invalid_amount", "Payment amount must be greater than zero.");

    public static Error InvalidDepositPortion => Error.Validation("payment.invalid_deposit_portion", "Deposit portion cannot be negative.");

    public static Error DepositExceedsAmount => Error.Validation("payment.deposit_exceeds_amount", "Deposit portion cannot exceed total payment amount.");

    public static Error InvalidState(string expected, PaymentStatus current) =>
        Error.Conflict("payment.invalid_state", $"Payment must be in {expected} state. Current status: {current}.");

    public static Error InvalidTransactionId => Error.Validation("payment.invalid_transaction_id", "Gateway transaction ID is required.");

    public static Error PaymentNotFound => Error.NotFound("payment.not_found", "Payment transaction was not found.");

    public static Error InvalidMethod => Error.Validation("payment.invalid_method", "Payment method must be VNPay, Paypal, Stripe, or Transfer.");

    public static Error InvalidRefundAmount => Error.Validation("payment.invalid_refund_amount", "Refund amount must be greater than zero.");

    public static Error RefundExceedsAmount => Error.Conflict("payment.refund_exceeds_amount", "Refund amount exceeds the remaining refundable payment amount.");

    public static Error RefundReasonRequired => Error.Validation("payment.refund_reason_required", "A reason is required to refund a payment.");

    public static Error MissingGatewayTransactionId => Error.Conflict("payment.missing_gateway_transaction_id", "Payment does not have a gateway transaction ID to refund.");

    public static Error InvalidReturnUrl => Error.Validation("payment.invalid_return_url", "A valid HTTP/HTTPS return URL is required.");

    public static Error BookingNotPayable => Error.Conflict("payment.booking_not_payable", "Booking must be in Confirmed status before payment can be initiated.");

    public static Error NotParticipant => Error.Forbidden("payment.not_participant", "You are not authorized to access this payment.");

    public static Error NotOwner => Error.Forbidden("payment.not_owner", "Only the vehicle owner or an admin can manage this deposit.");

    public static Error InvalidSignature => Error.Unauthorized("payment.invalid_signature", "Invalid payment gateway webhook signature.");

    public static Error WebhookInvalidOrder => Error.Validation("payment.invalid_order_id", "Webhook order ID is invalid.");

    public static Error AmountMismatch => Error.Validation("payment.amount_mismatch", "Webhook payment amount does not match the pending payment amount.");

    public static Error GatewayFailure => Error.Failure("payment.gateway_failure", "Payment gateway rejected the checkout request.");
}
