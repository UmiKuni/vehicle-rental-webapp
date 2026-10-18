using CarRental.BuildingBlocks.Domain;

namespace CarRental.BuildingBlocks.Messaging;

/// <summary>
/// Base record for integration events dispatched across module boundaries.
/// </summary>
public abstract record IntegrationEvent : DomainEvent, IIntegrationEvent
{
    protected IntegrationEvent(Guid eventId, DateTimeOffset occurredAt) : base(eventId, occurredAt)
    {
    }

    protected IntegrationEvent() : base()
    {
    }
}
