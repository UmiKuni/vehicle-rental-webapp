namespace CarRental.BuildingBlocks.Domain;

/// <summary>
/// Base record for domain events with an identifier and occurrence timestamp.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    protected DomainEvent(Guid eventId, DateTimeOffset occurredAt)
    {
        EventId = eventId;
        OccurredAt = occurredAt;
    }

    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
