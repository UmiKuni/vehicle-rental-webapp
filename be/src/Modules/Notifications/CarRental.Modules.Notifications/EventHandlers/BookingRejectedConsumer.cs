using CarRental.Modules.Bookings.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRejected"/>.
/// </summary>
public sealed class BookingRejectedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: BookingRejected event from Bookings when an owner rejects a reservation request.
     * 
     * Responsibilities:
     * 1. Target recipient: Customer (event.CustomerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.CustomerId).
     * 3. Construct deduplication key: $"booking-rejected:{event.EventId}:{event.CustomerId}".
     * 4. Build Notification explaining rejection reason provided by the owner.
     * 5. Dispatch Email/Push notification and persist status to NotificationsDbContext.
     */
    public Task Consume(BookingRejected @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

