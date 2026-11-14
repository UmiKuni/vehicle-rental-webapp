using CarRental.Modules.Fleet.Contracts.Events;
using Wolverine.Attributes;

namespace CarRental.Modules.Search.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="VehiclePublished"/>.
/// </summary>
public sealed class VehiclePublishedConsumer
{
    [WolverineIgnore]
    public Task Consume(VehiclePublished message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Task.CompletedTask;
    }

    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: VehiclePublished event from the Fleet module when an admin approves or updates a vehicle listing.
     * 
     * Responsibilities:
     * 1. Find existing VehicleSearchDocument by event.VehicleId or initialize a new one via VehicleSearchDocument.Create(event.VehicleId).
     * 2. Call document.ApplyPublished(
     *        event.Brand,
     *        event.Model,
     *        event.Seats,
     *        event.Transmission,
     *        event.Location,
     *        event.Address,
     *        event.OccurredAt).
     *    - Respects Last-Write-Wins (LWW) timestamp to prevent out-of-order event overwrites.
     *    - Marks IsPublished = true and updates PostGIS spatial location.
     * 3. Save or update the search document in SearchDbContext.
     */
}
