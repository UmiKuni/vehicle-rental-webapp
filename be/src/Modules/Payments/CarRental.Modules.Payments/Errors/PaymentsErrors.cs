using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Payments.Errors;

internal static class PaymentsErrors
{
    public static Error DepositInvalidBooking => Error.Validation("deposit.invalid_booking", "Booking ID is required.");

    public static Error DepositInvalidAmount => Error.Validation("deposit.invalid_amount", "Deposit amount cannot be negative.");

    public static Error Frozen => Error.Validation("deposit.frozen", "Cannot release a frozen deposit.");

    public static Error InvalidDeduction => Error.Validation("deposit.invalid_deduction", "Deduction amount must be greater than zero.");

    public static Error DeductionExceedsAmount => Error.Validation("deposit.deduction_exceeds_amount", "Deduction amount cannot exceed total deposit amount.");

    public static Error InvalidPercent => Error.Validation("deposit.invalid_percent", "Customer refund percentage must be between 0 and 100.");

    public static Error InvalidBooking => Error.Validation("payment.invalid_booking", "Booking ID is required.");

    public static Error InvalidAmount => Error.Validation("payment.invalid_amount", "Payment amount must be greater than zero.");

    public static Error InvalidDepositPortion => Error.Validation("payment.invalid_deposit_portion", "Deposit portion cannot be negative.");

    public static Error DepositExceedsAmount => Error.Validation("payment.deposit_exceeds_amount", "Deposit portion cannot exceed total payment amount.");

    public static Error InvalidState => Error.Validation("payment.invalid_state", "Payment method can only be updated for pending payments.");

    public static Error InvalidTransactionId => Error.Validation("payment.invalid_transaction_id", "Gateway transaction ID is required.");

}
