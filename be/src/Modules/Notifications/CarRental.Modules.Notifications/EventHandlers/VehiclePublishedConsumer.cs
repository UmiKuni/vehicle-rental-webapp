using CarRental.Modules.Fleet.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehiclePublished"/>.
/// </summary>
public sealed class VehiclePublishedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: VehiclePublished event from Fleet when an admin approves vehicle onboarding.
     * 
     * Responsibilities:
     * 1. Target recipient: Vehicle owner (event.OwnerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.OwnerId).
     * 3. Construct deduplication key: $"vehicle-published:{event.EventId}:{event.VehicleId}".
     * 4. Build Notification congratulating the owner that their vehicle listing is live and discoverable.
     * 5. Dispatch Email/Push notification and save to NotificationsDbContext.
     */
    public Task Consume(VehiclePublished @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

