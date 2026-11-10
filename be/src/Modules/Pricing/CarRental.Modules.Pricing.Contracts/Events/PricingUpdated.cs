using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Pricing.Contracts.Events;

public sealed record PricingUpdated : IIntegrationEvent
{
    public Guid EventId { get; }
    public DateTimeOffset OccurredAt { get; }
    public Guid VehicleId { get; }
    public decimal PriceFrom { get; }

    public PricingUpdated(Guid eventId, DateTimeOffset occurredAt, Guid vehicleId, decimal priceFrom)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
        VehicleId = vehicleId;
        PriceFrom = priceFrom;
    }
}
