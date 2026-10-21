using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class BookingsModuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset StartTime = Now.AddDays(2);
    private static readonly DateTimeOffset EndTime = Now.AddDays(5);
    private static readonly DateTimeRange ValidPeriod = new(StartTime, EndTime);
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();

    private static (List<BookingLineItem> lines, CancellationPolicySnapshot policy) CreateTestLinesAndPolicy(Guid bookingId)
    {
        var lines = new List<BookingLineItem>
        {
            new(Guid.NewGuid(), bookingId, BookingLineItemType.Rent, Money.Of(1_500_000)),
            new(Guid.NewGuid(), bookingId, BookingLineItemType.Deposit, Money.Of(5_000_000))
        };

        var policy = new CancellationPolicySnapshot(bookingId, "[]");
        return (lines, policy);
    }

    [Fact]
    public void Request_WithValidParameters_ShouldSucceedAndBePending()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);

        var result = Booking.Request(
            CustomerId,
            OwnerId,
            VehicleId,
            ValidPeriod,
            "123 Nguyen Hue, D1, HCMC",
            lines,
            policy,
            Now);

        result.IsSuccess.Should().BeTrue();
        var booking = result.Value;
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.CustomerId.Should().Be(CustomerId);
        booking.OwnerId.Should().Be(OwnerId);
        booking.VehicleId.Should().Be(VehicleId);
        booking.LineItems.Should().HaveCount(2);
        booking.StatusHistory.Should().HaveCount(1);
        booking.StatusHistory.First().Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Request_WhenCustomerIsOwner_ShouldFail()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);

        var result = Booking.Request(
            CustomerId,
            CustomerId, // Same as customer
            VehicleId,
            ValidPeriod,
            "123 Nguyen Hue, D1, HCMC",
            lines,
            policy,
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("booking.customer_is_owner");
    }

    [Fact]
    public void Request_WhenStartIsInThePast_ShouldFail()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var pastPeriod = new DateTimeRange(Now.AddHours(-1), Now.AddDays(1));

        var result = Booking.Request(
            CustomerId,
            OwnerId,
            VehicleId,
            pastPeriod,
            "123 Nguyen Hue, D1, HCMC",
            lines,
            policy,
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("booking.start_in_past");
    }

    [Fact]
    public void Request_WhenMissingRentOrDepositItem_ShouldFail()
    {
        var tempId = Guid.NewGuid();
        var onlyRentLines = new List<BookingLineItem>
        {
            new(Guid.NewGuid(), tempId, BookingLineItemType.Rent, Money.Of(1_000_000))
        };
        var policy = new CancellationPolicySnapshot(tempId, "[]");

        var result = Booking.Request(
            CustomerId,
            OwnerId,
            VehicleId,
            ValidPeriod,
            "123 Nguyen Hue, D1, HCMC",
            onlyRentLines,
            policy,
            Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("booking.missing_deposit_item");
    }

    [Fact]
    public void Approve_ByOwner_ShouldTransitionToConfirmed_AndSetPaymentDeadline()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;

        var approveResult = booking.Approve(OwnerId, Now);

        approveResult.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentDeadline.Should().Be(Now.AddHours(2));
        booking.StatusHistory.Should().HaveCount(2);
    }

    [Fact]
    public void Approve_ByNonOwner_ShouldFail()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;

        var approveResult = booking.Approve(Guid.NewGuid(), Now);

        approveResult.IsSuccess.Should().BeFalse();
        approveResult.Error.Code.Should().Be("booking.unauthorized");
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public void Reject_ByOwner_ShouldTransitionToRejected()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;

        var rejectResult = booking.Reject(OwnerId, "Car under unscheduled maintenance", Now);

        rejectResult.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Rejected);
    }

    [Fact]
    public void MarkPaid_FromConfirmed_ShouldTransitionToPaid()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;
        booking.Approve(OwnerId, Now);

        var paymentId = Guid.NewGuid();
        var paidResult = booking.MarkPaid(paymentId, Now);

        paidResult.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Paid);
    }

    [Fact]
    public void FullRentalTripFlow_FromPendingToCompleted_ShouldSucceed()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;

        booking.Approve(OwnerId, Now).IsSuccess.Should().BeTrue();
        booking.MarkPaid(Guid.NewGuid(), Now).IsSuccess.Should().BeTrue();
        booking.StartTrip(Now.AddDays(2)).IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.InProgress);

        booking.CompleteTrip(Now.AddDays(5)).IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Completed);

        var feeResult = booking.AddOverKmFee(Money.Of(250_000));
        feeResult.IsSuccess.Should().BeTrue();
        booking.LineItems.Should().HaveCount(3);
        booking.LineItems.Last().Type.Should().Be(BookingLineItemType.OverKmFee);
    }

    [Fact]
    public void Cancel_WhenPaidMoreThan48HoursBeforeStart_ShouldHaveZeroPenalty()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;
        booking.Approve(OwnerId, Now);
        booking.MarkPaid(Guid.NewGuid(), Now);

        // Cancel at Now (which is 48 hours before StartTime = Now + 2 days)
        var cancelResult = booking.Cancel(CustomerId, "Trip postponed", Now);

        cancelResult.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        cancelResult.Value.PenaltyAmount.Amount.Should().Be(0);
        cancelResult.Value.RefundableAmount.Amount.Should().Be(6_500_000); // 1.5M rent + 5M deposit
    }

    [Fact]
    public void AdminCancel_ShouldCancelNonTerminalBooking()
    {
        var tempId = Guid.NewGuid();
        var (lines, policy) = CreateTestLinesAndPolicy(tempId);
        var booking = Booking.Request(CustomerId, OwnerId, VehicleId, ValidPeriod, "HCMC", lines, policy, Now).Value;

        var cancelResult = booking.AdminCancel(Guid.NewGuid(), "Violates safety policy", Now);

        cancelResult.IsSuccess.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }
}
