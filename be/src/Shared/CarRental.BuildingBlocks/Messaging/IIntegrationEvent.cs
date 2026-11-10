using CarRental.BuildingBlocks.Domain;

namespace CarRental.BuildingBlocks.Messaging;

/// <summary>
/// Marker interface extending IDomainEvent for integration events dispatched across module boundaries via the outbox.
/// </summary>
public interface IIntegrationEvent : IDomainEvent
{
}
