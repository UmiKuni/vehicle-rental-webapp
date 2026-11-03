using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingConfirmed"/>.
/// </summary>
public sealed class BookingConfirmedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingConfirmed event from Bookings when an owner approves a reservation.
     * 
     * Responsibilities:
     * 1. Target recipient: Customer (event.CustomerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.CustomerId).
     * 3. Construct unique deduplication key: $"booking-confirmed:{event.EventId}:{event.CustomerId}".
     * 4. Build Notification payload containing payment checkout link and payment deadline.
     * 5. Dispatch transactional alert (Email / SMS) so customer can pay before hold expiration.
     * 6. Save notification to NotificationsDbContext.
     */
    public Task Consume(BookingConfirmed @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

