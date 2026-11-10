using CarRental.Modules.Fleet.Contracts.Events;

namespace CarRental.Modules.Search.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehicleUnpublished"/>.
/// </summary>
public sealed class VehicleUnpublishedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: VehicleUnpublished event from the Fleet module when an admin or owner unpublishes/hides a vehicle.
     * 
     * Responsibilities:
     * 1. Query the VehicleSearchDocument for event.VehicleId from SearchDbContext.
     * 2. If found, call document.ApplyUnpublished(event.OccurredAt).
     *    - Marks IsPublished = false with LWW timestamp check.
     *    - Effectively removes the vehicle from active public search results (IsVisible == false).
     * 3. Persist updates in SearchDbContext.
     */
    public Task Consume(VehicleUnpublished @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

