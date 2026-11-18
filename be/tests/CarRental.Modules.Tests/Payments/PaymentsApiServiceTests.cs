using System.Reflection;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Payments.Services;
using CarRental.Modules.Payments.Validators;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Payments;

public class PaymentsApiServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PaymentsErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(Payment).Assembly.GetType("CarRental.Modules.Payments.Errors.PaymentsErrors");
        errorsType.Should().NotBeNull();

        var fieldErrors = errorsType!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Name: f.Name, Error: (Error)f.GetValue(null)!));

        var propertyErrors = errorsType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Error))
            .Select(p => (Name: p.Name, Error: (Error)p.GetValue(null)!));

        var factoryErrors = new List<(string Name, Error Error)>();
        var invalidStateMethod = errorsType.GetMethod("InvalidState", BindingFlags.Public | BindingFlags.Static);
        invalidStateMethod.Should().NotBeNull();
        factoryErrors.Add(("InvalidState", (Error)invalidStateMethod!.Invoke(null, ["Pending", PaymentStatus.Success])!));

        var depositInvalidStateMethod = errorsType.GetMethod("DepositInvalidState", BindingFlags.Public | BindingFlags.Static);
        depositInvalidStateMethod.Should().NotBeNull();
        factoryErrors.Add(("DepositInvalidState", (Error)depositInvalidStateMethod!.Invoke(null, ["Held", DepositStatus.Released])!));

        var allErrors = fieldErrors.Concat(propertyErrors).Concat(factoryErrors).ToList();
        allErrors.Should().NotBeEmpty();

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex(
                "^(payment|payments|deposit)\\.[a-z_]+$",
                $"Error '{name}' must match ^(payment|payments|deposit).[a-z_]+$.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();

        var frozen = allErrors.Single(e => e.Name == "Frozen").Error;
        frozen.Code.Should().Be("deposit.frozen");
        frozen.Type.Should().Be(ErrorType.Conflict);

        var paymentNotFound = allErrors.Single(e => e.Name == "PaymentNotFound").Error;
        paymentNotFound.Code.Should().Be("payment.not_found");
        paymentNotFound.Type.Should().Be(ErrorType.NotFound);

        var depositNotFound = allErrors.Single(e => e.Name == "DepositNotFound").Error;
        depositNotFound.Code.Should().Be("deposit.not_found");
        depositNotFound.Type.Should().Be(ErrorType.NotFound);

        var invalidSignature = allErrors.Single(e => e.Name == "InvalidSignature").Error;
        invalidSignature.Code.Should().Be("payment.invalid_signature");
        invalidSignature.Type.Should().Be(ErrorType.Unauthorized);

        var notParticipant = allErrors.Single(e => e.Name == "NotParticipant").Error;
        notParticipant.Code.Should().Be("payment.not_participant");
        notParticipant.Type.Should().Be(ErrorType.Forbidden);

        var notOwner = allErrors.Single(e => e.Name == "NotOwner").Error;
        notOwner.Code.Should().Be("payment.not_owner");
        notOwner.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task InitiateCheckoutAsync_ShouldValidateBookingAndReuseExistingPendingPayment()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, ownerId, bookingId, "Confirmed", 2_500_000m, 5_000_000m);

        var badMethod = await harness.Service.InitiateCheckoutAsync(
            customerId,
            new InitiatePaymentRequest(bookingId, "Crypto", "https://app.test/return"));
        badMethod.IsFailure.Should().BeTrue();
        badMethod.Error.Code.Should().Be("payment.invalid_method");

        var wrongCustomer = await harness.Service.InitiateCheckoutAsync(
            Guid.NewGuid(),
            new InitiatePaymentRequest(bookingId, "VNPay", "https://app.test/return"));
        wrongCustomer.IsFailure.Should().BeTrue();
        wrongCustomer.Error.Code.Should().Be("payment.not_participant");

        var firstCheckout = await harness.Service.InitiateCheckoutAsync(
            customerId,
            new InitiatePaymentRequest(bookingId, "VNPay", "https://app.test/return"));
        firstCheckout.IsSuccess.Should().BeTrue();
        firstCheckout.Value.BookingId.Should().Be(bookingId);
        firstCheckout.Value.Amount.Should().Be(7_500_000m);
        firstCheckout.Value.Method.Should().Be(PaymentMethod.VNPay);
        harness.Payments.Should().ContainSingle();

        // Second checkout reuses same pending payment and updates method
        var secondCheckout = await harness.Service.InitiateCheckoutAsync(
            customerId,
            new InitiatePaymentRequest(bookingId, "Paypal", "https://app.test/return"));
        secondCheckout.IsSuccess.Should().BeTrue();
        secondCheckout.Value.PaymentId.Should().Be(firstCheckout.Value.PaymentId);
        secondCheckout.Value.Method.Should().Be(PaymentMethod.Paypal);
        harness.Payments.Should().ContainSingle();
    }

    [Fact]
    public async Task InitiateCheckoutAsync_WhenBookingNotConfirmed_ShouldReturnConflict()
    {
        var customerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, Guid.NewGuid(), bookingId, "Pending", 1_000_000m, 2_000_000m);

        var result = await harness.Service.InitiateCheckoutAsync(
            customerId,
            new InitiatePaymentRequest(bookingId, "Transfer", "https://app.test/return"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.booking_not_payable");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task HandleWebhookAsync_Success_ShouldMarkPaidHoldDepositPublishEventAndBeIdempotent()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, ownerId, bookingId, "Confirmed", 2_000_000m, 3_000_000m);

        var checkout = await harness.Service.CreateCheckoutAsync(
            bookingId,
            5_000_000m,
            3_000_000m,
            PaymentMethod.VNPay,
            "https://app.test/return");
        checkout.IsSuccess.Should().BeTrue();

        var invalidSig = await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("TXN-1", checkout.Value.PaymentId.ToString(), 5_000_000m, "00", "INVALID"));
        invalidSig.IsFailure.Should().BeTrue();
        invalidSig.Error.Code.Should().Be("payment.invalid_signature");

        var mismatchAmount = await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("TXN-1", checkout.Value.PaymentId.ToString(), 4_000_000m, "00", "sig"));
        mismatchAmount.IsFailure.Should().BeTrue();
        mismatchAmount.Error.Code.Should().Be("payment.amount_mismatch");

        var webhookResult = await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("TXN-1", checkout.Value.PaymentId.ToString(), 5_000_000m, "00", "valid-sig"));
        webhookResult.IsSuccess.Should().BeTrue();
        webhookResult.Value.Success.Should().BeTrue();

        var payment = harness.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Success);
        payment.GatewayTransactionId.Should().Be("TXN-1");

        harness.Deposits.Should().ContainSingle();
        var deposit = harness.Deposits.Single();
        deposit.BookingId.Should().Be(bookingId);
        deposit.Amount.Amount.Should().Be(3_000_000);
        deposit.Status.Should().Be(DepositStatus.Held);

        harness.WebhookInboxes.Should().ContainSingle(w => w.Id == "TXN-1");
        harness.BookingApiMock.Verify(
            b => b.MarkPaidAsync(bookingId, payment.Id.Value, Now, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            o => o.PublishAsync(
                It.Is<PaymentSucceeded>(e => e.BookingId == bookingId && e.PaymentId == payment.Id.Value && e.Amount.Amount == 5_000_000),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Duplicate webhook with same TransactionId is idempotent
        var duplicateResult = await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("TXN-1", checkout.Value.PaymentId.ToString(), 5_000_000m, "00", "valid-sig"));
        duplicateResult.IsSuccess.Should().BeTrue();
        duplicateResult.Value.Message.Should().Be("Webhook already processed.");
        harness.BookingApiMock.Verify(
            b => b.MarkPaidAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            o => o.PublishAsync(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhookAsync_FailureCode_ShouldMarkFailedAndPublishPaymentFailed()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, ownerId, bookingId, "Confirmed", 2_000_000m, 3_000_000m);

        var checkout = await harness.Service.CreateCheckoutAsync(
            bookingId,
            5_000_000m,
            3_000_000m,
            PaymentMethod.Stripe,
            "https://app.test/return");

        var webhookResult = await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("TXN-FAIL-1", checkout.Value.PaymentId.ToString(), 5_000_000m, "24", "valid-sig"));

        webhookResult.IsSuccess.Should().BeTrue();
        var payment = harness.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Failed);
        harness.Deposits.Should().BeEmpty();
        harness.BookingApiMock.Verify(
            b => b.MarkPaidAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.OutboxMock.Verify(
            o => o.PublishAsync(
                It.Is<PaymentFailed>(e => e.BookingId == bookingId && e.PaymentId == payment.Id.Value),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DepositEndpointsService_ShouldEnforceOwnerOrAdminAndDeductOrRelease()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, ownerId, bookingId, "Paid", 2_000_000m, 5_000_000m);

        var deposit = Deposit.Hold(bookingId, Money.Of(5_000_000), Now).Value;
        harness.Deposits.Add(deposit);

        // Customer cannot deduct deposit
        var customerDeduct = await harness.Service.DeductDepositByIdAsync(deposit.Id.Value, customerId, false, 500_000m, "Scratch");
        customerDeduct.IsFailure.Should().BeTrue();
        customerDeduct.Error.Code.Should().Be("payment.not_owner");

        // Zero amount rejected
        var invalidAmount = await harness.Service.DeductDepositByIdAsync(deposit.Id.Value, ownerId, false, 0m, "Invalid");
        invalidAmount.IsFailure.Should().BeTrue();
        invalidAmount.Error.Code.Should().Be("deposit.invalid_deduct_amount");

        // Owner deduct succeeds
        var ownerDeduct = await harness.Service.DeductDepositByIdAsync(deposit.Id.Value, ownerId, false, 1_200_000m, "Damage");
        ownerDeduct.IsSuccess.Should().BeTrue();
        ownerDeduct.Value.Status.Should().Be("Deducted");
        ownerDeduct.Value.DeductedAmount.Should().Be(1_200_000m);
        ownerDeduct.Value.RefundedAmount.Should().Be(3_800_000m);

        // Release after deduction fails with Conflict
        var releaseAfterDeduct = await harness.Service.ReleaseDepositByIdAsync(deposit.Id.Value, ownerId, false, "Done");
        releaseAfterDeduct.IsFailure.Should().BeTrue();
        releaseAfterDeduct.Error.Code.Should().Be("deposit.invalid_state");
    }

    [Fact]
    public async Task FreezeSettleAndGetDeposit_ShouldManageDisputeLifecycle()
    {
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(Guid.NewGuid(), Guid.NewGuid(), bookingId, "Paid", 2_000_000m, 4_000_000m);
        var deposit = Deposit.Hold(bookingId, Money.Of(4_000_000), Now).Value;
        harness.Deposits.Add(deposit);

        (await harness.Service.FreezeDepositAsync(bookingId)).IsSuccess.Should().BeTrue();
        deposit.IsFrozen.Should().BeTrue();

        var releaseFrozen = await harness.Service.ReleaseDepositAsync(bookingId);
        releaseFrozen.IsFailure.Should().BeTrue();
        releaseFrozen.Error.Code.Should().Be("deposit.frozen");

        (await harness.Service.SettleDepositAsync(bookingId, 75)).IsSuccess.Should().BeTrue();

        var details = await harness.Service.GetDepositAsync(bookingId);
        details.IsSuccess.Should().BeTrue();
        details.Value.Status.Should().Be(DepositStatus.Deducted);
        details.Value.DeductedAmount.Should().Be(1_000_000m);
        details.Value.IsFrozen.Should().BeFalse();
    }

    [Fact]
    public async Task RefundPaymentAsync_ShouldSupportPartialAndFullRefundsAndEnforceLimits()
    {
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var harness = CreateHarness(customerId, ownerId, bookingId, "Confirmed", 3_000_000m, 2_000_000m);

        var checkout = await harness.Service.CreateCheckoutAsync(
            bookingId,
            5_000_000m,
            2_000_000m,
            PaymentMethod.Stripe,
            "https://app.test/return");
        await harness.Service.HandleWebhookAsync(
            new PaymentWebhookRequest("STRIPE_TXN_1", checkout.Value.PaymentId.ToString(), 5_000_000m, "00", "valid-sig"));

        var paymentId = checkout.Value.PaymentId;

        // Customer cannot refund
        var customerRefund = await harness.Service.RefundPaymentAsync(paymentId, customerId, false, 1_000_000m, "Customer request");
        customerRefund.IsFailure.Should().BeTrue();
        customerRefund.Error.Code.Should().Be("payment.not_owner");

        // Partial refund succeeds
        var partial = await harness.Service.RefundPaymentAsync(paymentId, ownerId, false, 2_000_000m, "Early return");
        partial.IsSuccess.Should().BeTrue();
        partial.Value.Status.Should().Be("PartiallyRefunded");
        partial.Value.RefundedAmount.Should().Be(2_000_000m);
        partial.Value.RemainingAmount.Should().Be(3_000_000m);

        // Exceeding remaining amount fails
        var excessive = await harness.Service.RefundPaymentAsync(paymentId, ownerId, false, 3_500_000m, "Too much");
        excessive.IsFailure.Should().BeTrue();
        excessive.Error.Code.Should().Be("payment.refund_exceeds_amount");

        // Remaining full refund succeeds
        var full = await harness.Service.RefundPaymentAsync(paymentId, ownerId, false, 3_000_000m, "Booking cancelled");
        full.IsSuccess.Should().BeTrue();
        full.Value.Status.Should().Be("Refunded");
        full.Value.RefundedAmount.Should().Be(5_000_000m);
        full.Value.RemainingAmount.Should().Be(0m);

        harness.OutboxMock.Verify(
            o => o.PublishAsync(
                It.Is<PaymentRefunded>(e => e.PaymentId == paymentId && e.IsFullRefund),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RequestValidators_ShouldRejectInvalidInputsWithConformingErrorCodes()
    {
        var checkoutValidator = new InitiatePaymentRequestValidator();
        var badBooking = checkoutValidator.Validate(new InitiatePaymentRequest(Guid.Empty, "VNPay", "https://app.test/return"));
        badBooking.IsValid.Should().BeFalse();
        badBooking.Errors.Should().Contain(e => e.ErrorCode == "payment.invalid_booking");

        var badMethod = checkoutValidator.Validate(new InitiatePaymentRequest(Guid.NewGuid(), "Crypto", "https://app.test/return"));
        badMethod.IsValid.Should().BeFalse();
        badMethod.Errors.Should().Contain(e => e.ErrorCode == "payment.invalid_method");

        var badUrl = checkoutValidator.Validate(new InitiatePaymentRequest(Guid.NewGuid(), "VNPay", "ftp://app.test/return"));
        badUrl.IsValid.Should().BeFalse();
        badUrl.Errors.Should().Contain(e => e.ErrorCode == "payment.invalid_return_url");

        var refundValidator = new RefundPaymentRequestValidator();
        var badRefund = refundValidator.Validate(new RefundPaymentRequest(0m, " "));
        badRefund.IsValid.Should().BeFalse();
        badRefund.Errors.Should().Contain(e => e.ErrorCode == "payment.invalid_refund_amount");
        badRefund.Errors.Should().Contain(e => e.ErrorCode == "payment.refund_reason_required");

        var deductValidator = new DeductDepositRequestValidator();
        var badDeduct = deductValidator.Validate(new DeductDepositRequest(0m, "Scratch"));
        badDeduct.IsValid.Should().BeFalse();
        badDeduct.Errors.Should().Contain(e => e.ErrorCode == "deposit.invalid_deduct_amount");
    }

    private static (
        PaymentsApiService Service,
        List<Payment> Payments,
        List<Deposit> Deposits,
        List<WebhookInbox> WebhookInboxes,
        Mock<IBookingApi> BookingApiMock,
        Mock<IEventOutbox> OutboxMock) CreateHarness(
        Guid customerId,
        Guid ownerId,
        Guid bookingId,
        string bookingStatus,
        decimal totalRent,
        decimal depositAmount)
    {
        var payments = new List<Payment>();
        var deposits = new List<Deposit>();
        var inboxes = new List<WebhookInbox>();

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetPaymentByIdAsync(It.IsAny<PaymentId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentId id, CancellationToken _) => payments.SingleOrDefault(p => p.Id == id));
        repoMock.Setup(r => r.GetLatestPaymentByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, CancellationToken _) => payments.Where(p => p.BookingId == bId).OrderByDescending(p => p.CreatedAt).FirstOrDefault());
        repoMock.Setup(r => r.GetPendingPaymentByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, CancellationToken _) => payments.Where(p => p.BookingId == bId && p.Status == PaymentStatus.Pending).OrderByDescending(p => p.CreatedAt).FirstOrDefault());
        repoMock.Setup(r => r.GetDepositByIdAsync(It.IsAny<DepositId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DepositId id, CancellationToken _) => deposits.SingleOrDefault(d => d.Id == id));
        repoMock.Setup(r => r.GetDepositByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, CancellationToken _) => deposits.FirstOrDefault(d => d.BookingId == bId));
        repoMock.Setup(r => r.HasWebhookBeenProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string txnId, CancellationToken _) => inboxes.Any(w => w.Id == txnId));
        repoMock.Setup(r => r.AddPayment(It.IsAny<Payment>())).Callback((Payment p) => payments.Add(p));
        repoMock.Setup(r => r.AddDeposit(It.IsAny<Deposit>())).Callback((Deposit d) => deposits.Add(d));
        repoMock.Setup(r => r.AddWebhookInbox(It.IsAny<WebhookInbox>())).Callback((WebhookInbox w) => inboxes.Add(w));

        var bookingApiMock = new Mock<IBookingApi>();
        bookingApiMock.Setup(b => b.GetRentalDetailsAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new BookingRentalDetailsDto(
                bookingId,
                customerId,
                ownerId,
                Guid.NewGuid(),
                bookingStatus,
                Now.AddDays(1),
                Now.AddDays(3),
                totalRent,
                depositAmount)));
        bookingApiMock.Setup(b => b.IsParticipantAsync(bookingId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid actor, CancellationToken _) => actor == customerId || actor == ownerId);
        bookingApiMock.Setup(b => b.MarkPaidAsync(bookingId, It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var gatewayMock = new Mock<IPaymentGateway>();
        gatewayMock.Setup(g => g.CreateCheckoutSession(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<PaymentMethod>(),
                It.IsAny<string>(),
                It.IsAny<DateTimeOffset>()))
            .Returns((Guid pId, Guid _, decimal amt, PaymentMethod m, string _, DateTimeOffset n) =>
                ($"https://sandbox.gateway/{m}/{pId:N}?amount={amt}",
                 $"https://qr.gateway/{pId:N}",
                 n.AddMinutes(15)));
        gatewayMock.Setup(g => g.VerifyWebhookSignature(It.IsAny<PaymentWebhookRequest>()))
            .Returns((PaymentWebhookRequest req) => !string.IsNullOrWhiteSpace(req.Signature) && req.Signature != "INVALID");
        gatewayMock.Setup(g => g.RefundAsync(
                It.IsAny<Guid>(),
                It.IsAny<PaymentMethod>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid pId, PaymentMethod m, string _, decimal amt, string _, CancellationToken _) =>
                Result.Success(new GatewayRefundResult($"REF-{m}-{pId:N}", amt, Now)));

        var outboxMock = new Mock<IEventOutbox>();
        outboxMock.Setup(o => o.PublishAsync(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        outboxMock.Setup(o => o.PublishAsync(It.IsAny<PaymentFailed>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        outboxMock.Setup(o => o.PublishAsync(It.IsAny<PaymentRefunded>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new PaymentsApiService(
            dbContextMock.Object,
            repoMock.Object,
            bookingApiMock.Object,
            gatewayMock.Object,
            outboxMock.Object,
            clockMock.Object);

        return (service, payments, deposits, inboxes, bookingApiMock, outboxMock);
    }
}
