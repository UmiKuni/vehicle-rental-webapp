using CarRental.Modules.Support.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="TicketResolved"/>.
/// </summary>
public sealed class TicketResolvedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: TicketResolved event from Support when an admin completes dispute investigation and issues a ruling.
     * 
     * Responsibilities:
     * 1. Target recipients: Both disputing parties (customer and vehicle owner involved in event.BookingId).
     * 2. Construct deduplication keys for each recipient.
     * 3. Build Notification explaining the final ruling:
     *    - Deposit split percentage (event.DepositToCustomerPercent).
     *    - Resolution summary and refund/deduction amounts.
     * 4. Dispatch Email and in-app alerts to both parties.
     * 5. Record delivery status in NotificationsDbContext.
     */
    public Task Consume(TicketResolved @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

