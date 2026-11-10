namespace CarRental.BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots in DDD.
/// Encapsulates invariants, manages domain events, and provides optimistic concurrency token.
/// </summary>
/// <typeparam name="TId">The type of the aggregate root's identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Concurrency token mapped to PostgreSQL's xmin system column.
    /// </summary>
    public uint Version { get; private set; }

    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    protected AggregateRoot()
    {
    }

    /// <summary>
    /// Records a domain event to be dispatched when the aggregate is saved.
    /// </summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Clears all recorded domain events after dispatch.
    /// </summary>
    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
