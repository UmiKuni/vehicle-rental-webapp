namespace CarRental.BuildingBlocks.Messaging;

/// <summary>
/// Storage contract for tracking processed event IDs per consumer to ensure idempotent processing.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Checks whether an event has already been successfully processed by the specified consumer.
    /// </summary>
    Task<bool> HasBeenProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an event as successfully processed by the specified consumer.
    /// </summary>
    Task MarkAsProcessedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base class for message and integration event handlers requiring at-most-once/exactly-once processing guarantees.
/// </summary>
/// <typeparam name="TEvent">The type of integration event.</typeparam>
public abstract class IdempotentHandler<TEvent> where TEvent : class, IIntegrationEvent
{
    private readonly IIdempotencyStore _idempotencyStore;

    protected IdempotentHandler(IIdempotencyStore idempotencyStore)
    {
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
    }

    /// <summary>
    /// Coordinates idempotent execution by checking if the event was previously handled.
    /// </summary>
    public async Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var consumerName = GetType().FullName ?? GetType().Name;
        if (await _idempotencyStore.HasBeenProcessedAsync(@event.EventId, consumerName, cancellationToken))
        {
            return;
        }

        await HandleCoreAsync(@event, cancellationToken);
        await _idempotencyStore.MarkAsProcessedAsync(@event.EventId, consumerName, cancellationToken);
    }

    /// <summary>
    /// Module-specific business logic for handling the integration event.
    /// </summary>
    protected abstract Task HandleCoreAsync(TEvent @event, CancellationToken cancellationToken);
}
