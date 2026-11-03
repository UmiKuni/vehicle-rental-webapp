using CarRental.Modules.Payments.Contracts.Events;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentSucceeded"/>.
/// </summary>
public sealed class PaymentSucceededConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: PaymentSucceeded event from the Payments module when checkout transaction completes.
     * 
     * Responsibilities:
     * 1. Check idempotency: Query ProcessedEvents table in ReportingDbContext by event.EventId.
     *    - If already processed, exit early.
     * 2. Compute the UTC+7 business date bucket from event.OccurredAt via DailyBookingStats.ToBucketDate().
     * 3. Query existing DailyBookingStats for the date bucket or initialize a new one via DailyBookingStats.Create(date).
     * 4. Call stats.ApplyPaymentSucceeded(event.Amount.Amount, event.OccurredAt) to accumulate TotalRevenue.
     * 5. Record event.EventId in ProcessedEvents table.
     * 6. Persist changes to ReportingDbContext.
     */
    public Task Consume(PaymentSucceeded @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

