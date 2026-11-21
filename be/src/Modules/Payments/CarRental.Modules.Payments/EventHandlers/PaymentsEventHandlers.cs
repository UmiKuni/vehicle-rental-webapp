using System.Text.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Payments.Services;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Payments.EventHandlers;

[WolverineHandler]
public sealed class PaymentsEventHandlers
{
    public Task Handle(BookingCancelled message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IPaymentsRepository>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetService<IPaymentGateway>(),
            services.GetService<IEventOutbox>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    public Task Handle(BookingExpired message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IPaymentsRepository>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    public Task Handle(HandoverConfirmed message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IPaymentsRepository>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    internal Task Handle(
        BookingCancelled message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct) =>
        Handle(message, repository, dbContext, gateway: null, outbox: null, clock, ct);

    internal async Task Handle(
        BookingCancelled message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        IPaymentGateway? gateway,
        IEventOutbox? outbox,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (message.BookingId == Guid.Empty)
            return;

        var now = clock.GetUtcNow();
        var changed = false;
        PaymentRefunded? refundedEvent = null;

        var pendingPayment = await repository.GetPendingPaymentByBookingIdAsync(message.BookingId, ct);
        if (pendingPayment is not null)
        {
            var cancelResult = pendingPayment.Cancel(message.Reason, now);
            if (cancelResult.IsSuccess)
                changed = true;
        }

        if (message.RefundableAmount is not null && message.RefundableAmount.Amount > 0 && gateway is not null)
        {
            var refundInboxKey = message.EventId != Guid.Empty ? $"booking-cancelled:{message.EventId:N}" : null;
            var alreadyRefunded = refundInboxKey is not null
                && await repository.HasWebhookBeenProcessedAsync(refundInboxKey, ct);

            if (!alreadyRefunded)
            {
                var latestPayment = await repository.GetLatestPaymentByBookingIdAsync(message.BookingId, ct);
                if (latestPayment is not null
                    && latestPayment.Status is PaymentStatus.Success or PaymentStatus.PartiallyRefunded
                    && !string.IsNullOrWhiteSpace(latestPayment.GatewayTransactionId))
                {
                    var remainingRefundable = latestPayment.Amount.Amount - (latestPayment.RefundedAmount?.Amount ?? 0L);
                    var refundAmount = Math.Min(message.RefundableAmount.Amount, remainingRefundable);
                    if (refundAmount > 0)
                    {
                        var refundReason = string.IsNullOrWhiteSpace(message.Reason)
                            ? "Booking cancelled"
                            : message.Reason.Trim();

                        var gatewayResult = await gateway.RefundAsync(
                            new GatewayRefundRequest(
                                latestPayment.Id.Value,
                                latestPayment.Method,
                                latestPayment.GatewayTransactionId,
                                refundAmount,
                                latestPayment.Amount.Amount,
                                latestPayment.Amount.Currency,
                                refundReason,
                                latestPayment.CompletedAt),
                            ct);

                        if (gatewayResult.IsSuccess)
                        {
                            var refundMoney = Money.Of(refundAmount, latestPayment.Amount.Currency);
                            var refundResult = latestPayment.Refund(
                                refundMoney,
                                gatewayResult.Value.RefundTransactionId,
                                refundReason,
                                gatewayResult.Value.ProcessedAt);

                            if (refundResult.IsSuccess)
                            {
                                if (refundInboxKey is not null)
                                {
                                    var payloadJson = JsonSerializer.Serialize(new
                                    {
                                        Event = nameof(BookingCancelled),
                                        message.EventId,
                                        message.BookingId,
                                        PaymentId = latestPayment.Id.Value,
                                        RefundAmount = refundAmount
                                    });
                                    repository.AddWebhookInbox(new WebhookInbox(refundInboxKey, now, payloadJson));
                                }

                                changed = true;
                                refundedEvent = new PaymentRefunded(
                                    Guid.CreateVersion7(),
                                    gatewayResult.Value.ProcessedAt,
                                    latestPayment.BookingId,
                                    latestPayment.Id.Value,
                                    refundMoney,
                                    latestPayment.Status == PaymentStatus.Refunded,
                                    refundReason);
                            }
                        }
                    }
                }
            }
        }

        var deposit = await repository.GetDepositByBookingIdAsync(message.BookingId, ct);
        if (deposit is not null && deposit.Status == DepositStatus.Held)
        {
            var releaseResult = deposit.Release(now);
            if (releaseResult.IsSuccess)
                changed = true;
        }

        if (changed)
            await dbContext.SaveChangesAsync(ct);

        if (refundedEvent is not null && outbox is not null)
            await outbox.PublishAsync(refundedEvent, ct);
    }

    internal async Task Handle(
        BookingExpired message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (message.BookingId == Guid.Empty)
            return;

        var pendingPayment = await repository.GetPendingPaymentByBookingIdAsync(message.BookingId, ct);
        if (pendingPayment is null)
            return;

        var cancelResult = pendingPayment.Cancel(message.Reason, clock.GetUtcNow());
        if (cancelResult.IsSuccess)
            await dbContext.SaveChangesAsync(ct);
    }

    internal async Task Handle(
        HandoverConfirmed message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (message.BookingId == Guid.Empty)
            return;

        if (!string.Equals(message.Type, "Return", StringComparison.OrdinalIgnoreCase))
            return;

        var deposit = await repository.GetDepositByBookingIdAsync(message.BookingId, ct);
        if (deposit is null || deposit.Status != DepositStatus.Held)
            return;

        var now = clock.GetUtcNow();
        deposit.SetReleaseAfter(now.AddHours(48));
        await dbContext.SaveChangesAsync(ct);
    }
}
