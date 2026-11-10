using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingCancelled"/>.
/// </summary>
public sealed class BookingCancelledConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingCancelled event from Bookings when a trip is cancelled.
     * 
     * Responsibilities:
     * 1. Target recipients: Both Customer (event.CustomerId) and Owner (event.OwnerId).
     * 2. Query contact info via IUsersApi for both parties.
     * 3. Construct individual deduplication keys:
     *    - $"booking-cancelled:{event.EventId}:{event.CustomerId}"
     *    - $"booking-cancelled:{event.EventId}:{event.OwnerId}"
     * 4. Create Notification records summarizing cancellation cause, penalty fee, and refund breakdown.
     * 5. Dispatch alerts and persist to NotificationsDbContext.
     */
    public Task Consume(BookingCancelled @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

