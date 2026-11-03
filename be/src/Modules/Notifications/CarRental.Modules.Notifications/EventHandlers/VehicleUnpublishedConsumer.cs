using CarRental.Modules.Fleet.Contracts.Events;

namespace CarRental.Modules.Notifications.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehicleUnpublished"/>.
/// </summary>
public sealed class VehicleUnpublishedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: VehicleUnpublished event from Fleet when a vehicle listing is taken down.
     * 
     * Responsibilities:
     * 1. Target recipient: Vehicle owner (event.OwnerId).
     * 2. Query contact info via IUsersApi.GetContactAsync(event.OwnerId).
     * 3. Construct deduplication key: $"vehicle-unpublished:{event.EventId}:{event.VehicleId}".
     * 4. Build Notification explaining reason for unpublishing and instructions on required updates/resubmission.
     * 5. Dispatch Email notification and record in NotificationsDbContext.
     */
    public Task Consume(VehicleUnpublished @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

