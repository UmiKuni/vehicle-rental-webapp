using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class PaymentsModuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid BookingId = Guid.NewGuid();

    [Fact]
    public void CreatePending_WithValidParameters_ShouldSucceed()
    {
        var totalAmount = Money.Of(6_500_000);
        var depositPortion = Money.Of(5_000_000);

        var result = Payment.CreatePending(BookingId, totalAmount, depositPortion, Now);

        result.IsSuccess.Should().BeTrue();
        var payment = result.Value;
        payment.BookingId.Should().Be(BookingId);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Amount.Should().Be(6_500_000);
        payment.DepositPortion.Amount.Should().Be(5_000_000);
    }

    [Fact]
    public void CreatePending_WhenDepositExceedsAmount_ShouldFail()
    {
        var totalAmount = Money.Of(1_000_000);
        var depositPortion = Money.Of(2_000_000);

        var result = Payment.CreatePending(BookingId, totalAmount, depositPortion, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("payment.deposit_exceeds_amount");
    }

    [Fact]
    public void MarkSuccess_FromPending_ShouldSucceed()
    {
        var payment = Payment.CreatePending(BookingId, Money.Of(1_000_000), Money.Zero(), Now).Value;

        var result = payment.MarkSuccess("VNPAY_TXN_998877", Now.AddMinutes(5));

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Success);
        payment.GatewayTransactionId.Should().Be("VNPAY_TXN_998877");
        payment.CompletedAt.Should().Be(Now.AddMinutes(5));
    }

    [Fact]
    public void MarkSuccess_WithEmptyTransactionId_ShouldFail()
    {
        var payment = Payment.CreatePending(BookingId, Money.Of(1_000_000), Money.Zero(), Now).Value;

        var result = payment.MarkSuccess("", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("payment.invalid_transaction_id");
    }

    [Fact]
    public void MarkFailed_FromPending_ShouldSucceed()
    {
        var payment = Payment.CreatePending(BookingId, Money.Of(1_000_000), Money.Zero(), Now).Value;

        var result = payment.MarkFailed("MOMO_FAIL_01", "Insufficient funds", Now);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().Be("Insufficient funds");
    }

    [Fact]
    public void HoldDeposit_WithValidParameters_ShouldBeHeldAndUnfrozen()
    {
        var result = Deposit.Hold(BookingId, Money.Of(5_000_000), Now);

        result.IsSuccess.Should().BeTrue();
        var deposit = result.Value;
        deposit.Status.Should().Be(DepositStatus.Held);
        deposit.Amount.Amount.Should().Be(5_000_000);
        deposit.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void ReleaseDeposit_WhenNotFrozen_ShouldTransitionToReleased()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;

        var result = deposit.Release(Now.AddDays(2), "  All good  ");

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Released);
        deposit.Notes.Should().Be("All good");
    }

    [Fact]
    public void ReleaseDeposit_WithWhitespaceNote_ShouldNormalizeNotesToNull()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;

        var result = deposit.Release(Now.AddDays(2), "   ");

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Released);
        deposit.Notes.Should().BeNull();
    }

    [Fact]
    public void ReleaseDeposit_WhenFrozen_ShouldFail()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;
        deposit.Freeze();

        var result = deposit.Release(Now.AddDays(2));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("deposit.frozen");
        deposit.Status.Should().Be(DepositStatus.Held);
    }

    [Fact]
    public void DeductDeposit_WithValidAmount_ShouldTransitionToDeducted()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;

        var result = deposit.Deduct(Money.Of(1_500_000), Now.AddDays(2), "  Minor scratch  ");

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Deducted);
        deposit.DeductedAmount.Amount.Should().Be(1_500_000);
        deposit.Notes.Should().Be("Minor scratch");
    }

    [Fact]
    public void DeductDeposit_ExceedingTotal_ShouldFail()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;

        var result = deposit.Deduct(Money.Of(6_000_000), Now.AddDays(2));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("deposit.deduction_exceeds_amount");
    }

    [Fact]
    public void ForfeitDeposit_ShouldDeductFullAmount()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;

        var result = deposit.Forfeit(Now.AddDays(2));

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Forfeited);
        deposit.DeductedAmount.Amount.Should().Be(5_000_000);
    }

    [Fact]
    public void SettleByRuling_With100Percent_ShouldRelease()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;
        deposit.Freeze();

        var result = deposit.SettleByRuling(100, Now);

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Released);
        deposit.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void SettleByRuling_WithZeroPercent_ShouldForfeit()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(5_000_000), Now).Value;
        deposit.Freeze();

        var result = deposit.SettleByRuling(0, Now);

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Forfeited);
        deposit.DeductedAmount.Amount.Should().Be(5_000_000);
        deposit.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void SettleByRuling_With70PercentToCustomer_ShouldDeduct30Percent()
    {
        var deposit = Deposit.Hold(BookingId, Money.Of(10_000_000), Now).Value;
        deposit.Freeze();

        // 70% to customer => 30% deducted for damages/owner
        var result = deposit.SettleByRuling(70, Now);

        result.IsSuccess.Should().BeTrue();
        deposit.Status.Should().Be(DepositStatus.Deducted);
        deposit.DeductedAmount.Amount.Should().Be(3_000_000);
        deposit.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public void CancelPendingPayment_ShouldTransitionToFailedAndBlockSubsequentSuccess()
    {
        var payment = Payment.CreatePending(BookingId, Money.Of(2_000_000), Money.Of(1_000_000), Now).Value;
        payment.SetPaymentMethod(PaymentMethod.VNPay).IsSuccess.Should().BeTrue();

        var cancelResult = payment.Cancel("Booking cancelled", Now.AddMinutes(5));
        cancelResult.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);

        var markSuccess = payment.MarkSuccess("TXN-LATE", Now.AddMinutes(6));
        markSuccess.IsFailure.Should().BeTrue();
        markSuccess.Error.Code.Should().Be("payment.invalid_state");
        markSuccess.Error.Type.Should().Be(ErrorType.Conflict);
    }
}
