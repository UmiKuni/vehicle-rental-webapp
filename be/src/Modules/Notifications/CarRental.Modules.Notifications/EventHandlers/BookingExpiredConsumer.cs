using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingExpired"/>.
/// </summary>
public sealed class BookingExpiredConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingExpired event from Bookings when a hold or payment window expires.
     * 
     * Responsibilities:
     * 1. Target recipient: Customer (event.CustomerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.CustomerId).
     * 3. Construct deduplication key: $"booking-expired:{event.EventId}:{event.CustomerId}".
     * 4. Build Notification explaining that the booking expired due to timeout and vehicle hold has been released.
     * 5. Dispatch notification and save to NotificationsDbContext.
     */
    public Task Consume(BookingExpired @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

