using CarRental.Modules.Pricing.Contracts.Events;

namespace CarRental.Modules.Search.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PricingUpdated"/>.
/// </summary>
public sealed class PricingUpdatedConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: PricingUpdated event from the Pricing module when base rates or daily pricing change.
     * 
     * Responsibilities:
     * 1. Find existing VehicleSearchDocument by event.VehicleId or initialize one via VehicleSearchDocument.Create(event.VehicleId).
     * 2. Call document.ApplyPrice(event.PriceFrom.Amount, event.OccurredAt).
     *    - Updates starting rental rate for faceted filtering and sorting.
     *    - Uses LWW timestamp check (LastPricingEventAt) to ensure eventual consistency.
     * 3. Persist changes to SearchDbContext.
     */
    public Task Consume(PricingUpdated @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

