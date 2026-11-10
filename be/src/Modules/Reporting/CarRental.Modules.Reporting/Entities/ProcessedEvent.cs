using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reporting.Entities;

/// <summary>
/// Tracks processed integration events to guarantee idempotent projections in the reporting read model.
/// </summary>
public sealed class ProcessedEvent : Entity<Guid>
{
    public Guid EventId => Id;
    public DateTimeOffset ProcessedAt { get; private set; }

    private ProcessedEvent()
    {
    }

    private ProcessedEvent(Guid eventId, DateTimeOffset processedAt) : base(eventId)
    {
        ProcessedAt = processedAt;
    }

    public static ProcessedEvent Create(Guid eventId, DateTimeOffset? processedAt = null)
    {
        return new ProcessedEvent(eventId, processedAt ?? DateTimeOffset.UtcNow);
    }
}
