using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Payments.EventHandlers;
using CarRental.Modules.Payments.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Payments;

public class PaymentsEventHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleBookingCancelled_ShouldCancelPendingPaymentAndReleaseHeldDeposit()
    {
        var bookingId = Guid.NewGuid();
        var pendingPayment = Payment.CreatePending(bookingId, Money.Of(3_000_000), Money.Of(2_000_000), Now).Value;
        var heldDeposit = Deposit.Hold(bookingId, Money.Of(2_000_000), Now).Value;

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now.AddMinutes(10));

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetPendingPaymentByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingPayment);
        repoMock.Setup(r => r.GetDepositByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldDeposit);

        var handler = new PaymentsEventHandlers();
        await handler.Handle(
            new BookingCancelled(Guid.NewGuid(), Now.AddMinutes(10), bookingId, Guid.NewGuid(), Money.Zero(), Money.Of(2_000_000), "Customer cancelled"),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);

        pendingPayment.Status.Should().Be(PaymentStatus.Failed);
        pendingPayment.FailureReason.Should().Be("Customer cancelled");
        heldDeposit.Status.Should().Be(DepositStatus.Released);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleBookingCancelled_WhenPaidWithRefundableAmount_ShouldAutoRefundIdempotentlyAndPublishEvent()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var paidPayment = Payment.CreatePending(bookingId, Money.Of(7_000_000), Money.Of(5_000_000), Now).Value;
        paidPayment.SetPaymentMethod(PaymentMethod.VNPay);
        paidPayment.MarkSuccess("VNP-TXN-100", Now.AddMinutes(5));

        var heldDeposit = Deposit.Hold(bookingId, Money.Of(5_000_000), Now.AddMinutes(5)).Value;
        var processedInbox = new HashSet<string>(StringComparer.Ordinal);

        var cancelTime = Now.AddMinutes(20);
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(cancelTime);

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetPendingPaymentByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Payment?)null);
        repoMock.Setup(r => r.GetLatestPaymentByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paidPayment);
        repoMock.Setup(r => r.GetDepositByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(heldDeposit);
        repoMock.Setup(r => r.HasWebhookBeenProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => processedInbox.Contains(id));
        repoMock.Setup(r => r.AddWebhookInbox(It.IsAny<WebhookInbox>()))
            .Callback((WebhookInbox w) => processedInbox.Add(w.Id));

        var gatewayMock = new Mock<IPaymentGateway>();
        gatewayMock.Setup(g => g.RefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new GatewayRefundResult("REF-VNP-100", 6_400_000m, cancelTime)));

        var outboxMock = new Mock<IEventOutbox>();
        outboxMock.Setup(o => o.PublishAsync(It.IsAny<PaymentRefunded>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var message = new BookingCancelled(
            eventId,
            cancelTime,
            bookingId,
            Guid.NewGuid(),
            Money.Of(600_000),
            Money.Of(6_400_000),
            "Cancelled within 48h");

        var handler = new PaymentsEventHandlers();
        await handler.Handle(
            message,
            repoMock.Object,
            dbContextMock.Object,
            gatewayMock.Object,
            outboxMock.Object,
            clockMock.Object,
            CancellationToken.None);

        paidPayment.Status.Should().Be(PaymentStatus.PartiallyRefunded);
        paidPayment.RefundedAmount.Amount.Should().Be(6_400_000);
        paidPayment.RefundTransactionId.Should().Be("REF-VNP-100");
        heldDeposit.Status.Should().Be(DepositStatus.Released);
        processedInbox.Should().Contain($"booking-cancelled:{eventId:N}");

        gatewayMock.Verify(
            g => g.RefundAsync(
                It.Is<GatewayRefundRequest>(r =>
                    r.PaymentId == paidPayment.Id.Value
                    && r.Amount == 6_400_000m
                    && r.GatewayTransactionId == "VNP-TXN-100"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        outboxMock.Verify(
            o => o.PublishAsync(
                It.Is<PaymentRefunded>(e =>
                    e.BookingId == bookingId
                    && e.PaymentId == paidPayment.Id.Value
                    && e.RefundedAmount.Amount == 6_400_000
                    && !e.IsFullRefund),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Replay -> idempotent, no second gateway refund
        await handler.Handle(
            message,
            repoMock.Object,
            dbContextMock.Object,
            gatewayMock.Object,
            outboxMock.Object,
            clockMock.Object,
            CancellationToken.None);

        gatewayMock.Verify(g => g.RefundAsync(It.IsAny<GatewayRefundRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        outboxMock.Verify(o => o.PublishAsync(It.IsAny<PaymentRefunded>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleBookingExpired_ShouldCancelLingeringPendingPayment()
    {
        var bookingId = Guid.NewGuid();
        var pendingPayment = Payment.CreatePending(bookingId, Money.Of(3_000_000), Money.Of(2_000_000), Now).Value;

        var expireTime = Now.AddHours(2);
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(expireTime);

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetPendingPaymentByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingPayment);

        var handler = new PaymentsEventHandlers();
        await handler.Handle(
            new BookingExpired(Guid.NewGuid(), expireTime, bookingId, "Payment deadline exceeded"),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);

        pendingPayment.Status.Should().Be(PaymentStatus.Failed);
        pendingPayment.FailureReason.Should().Be("Payment deadline exceeded");
        pendingPayment.CompletedAt.Should().Be(expireTime);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleHandoverConfirmed_Return_ShouldSetReleaseAfter48Hours()
    {
        var bookingId = Guid.NewGuid();
        var deposit = Deposit.Hold(bookingId, Money.Of(5_000_000), Now).Value;

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetDepositByBookingIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deposit);

        var handler = new PaymentsEventHandlers();

        // Pickup should not schedule release
        await handler.Handle(
            new HandoverConfirmed(Guid.NewGuid(), Now, bookingId, "Pickup", 10000, 80, Now),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);
        deposit.ReleaseAfter.Should().BeNull();
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);

        // Return should schedule release +48h
        await handler.Handle(
            new HandoverConfirmed(Guid.NewGuid(), Now, bookingId, "Return", 10250, 75, Now, 250),
            repoMock.Object,
            dbContextMock.Object,
            clockMock.Object,
            CancellationToken.None);
        deposit.ReleaseAfter.Should().Be(Now.AddHours(48));
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DepositAutoReleaseService_ShouldReleaseDueUnfrozenDeposits()
    {
        var currentTime = Now.AddHours(49);
        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(currentTime);

        var dueDeposit = Deposit.Hold(Guid.NewGuid(), Money.Of(5_000_000), Now).Value;
        dueDeposit.SetReleaseAfter(Now.AddHours(48));

        var repoMock = new Mock<IPaymentsRepository>();
        repoMock.Setup(r => r.GetDueDepositsForReleaseAsync(currentTime, It.IsAny<CancellationToken>()))
            .ReturnsAsync([dueDeposit]);

        var dbContextMock = new Mock<PaymentsDbContext>(new DbContextOptionsBuilder<PaymentsDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var services = new ServiceCollection();
        services.AddScoped<IPaymentsRepository>(_ => repoMock.Object);
        services.AddScoped<PaymentsDbContext>(_ => dbContextMock.Object);
        using var provider = services.BuildServiceProvider();

        var backgroundService = new DepositAutoReleaseService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            clockMock.Object,
            NullLogger<DepositAutoReleaseService>.Instance);

        var releasedCount = await backgroundService.ReleaseDueDepositsOnceAsync(CancellationToken.None);

        releasedCount.Should().Be(1);
        dueDeposit.Status.Should().Be(DepositStatus.Released);
        dueDeposit.SettledAt.Should().Be(currentTime);
        dbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
