using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Enums;
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
