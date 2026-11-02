using CarRental.Modules.Payments.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentSucceeded"/>.
/// </summary>
public sealed class PaymentSucceededConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: PaymentSucceeded event from Payments when transaction is settled via VNPay/MoMo/gateway.
     * 
     * Responsibilities:
     * 1. Target recipients: Both Customer and Owner.
     * 2. Construct deduplication keys for each recipient.
     * 3. Customer notification: Payment receipt with transaction reference, amount paid, and deposit held.
     * 4. Owner notification: Confirmation that rental fee and deposit have been secured, preparing for vehicle handover.
     * 5. Dispatch multi-channel notifications (Email & SMS) and record in NotificationsDbContext.
     */
    public Task Consume(PaymentSucceeded @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

