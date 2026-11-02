using CarRental.Modules.Payments.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentFailed"/>.
/// </summary>
public sealed class PaymentFailedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: PaymentFailed event from Payments when gateway reports a declined/failed charge.
     * 
     * Responsibilities:
     * 1. Target recipient: Customer attempting the payment.
     * 2. Construct deduplication key: $"payment-failed:{event.EventId}:{event.PaymentId}".
     * 3. Send Notification alert informing customer of failure reason (event.Reason) and providing a retry link.
     * 4. Persist delivery record to NotificationsDbContext.
     */
    public Task Consume(PaymentFailed @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

