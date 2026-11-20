using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Reporting.Entities;

/// <summary>
/// Tracks processed integration events to guarantee idempotent projections in the reporting read model.
/// </summary>
internal sealed class ProcessedEvent : Entity<Guid>
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

    public static ProcessedEvent Create(Guid eventId, DateTimeOffset processedAt)
    {
        return new ProcessedEvent(eventId, processedAt);
    }
}
