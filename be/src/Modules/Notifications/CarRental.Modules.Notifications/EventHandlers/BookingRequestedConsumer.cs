using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRequested"/>.
/// </summary>
public sealed class BookingRequestedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingRequested event from Bookings when a customer places a new booking request.
     * 
     * Responsibilities:
     * 1. Target recipient: Vehicle owner (event.OwnerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.OwnerId).
     * 3. Construct unique deduplication key: $"booking-requested:{event.EventId}:{event.OwnerId}".
     * 4. Create Notification record (Email / Push) alerting the owner of a pending reservation requiring approval.
     * 5. Dispatch message via email/push notification sender and call notification.MarkSent() or MarkFailed().
     * 6. Persist notification audit trail to NotificationsDbContext.
     */
    public Task Consume(BookingRequested @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

