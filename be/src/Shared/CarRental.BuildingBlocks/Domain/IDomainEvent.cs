namespace CarRental.BuildingBlocks.Domain;

/// <summary>
/// Represents a domain event that records an occurrence of something significant in the domain.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}
