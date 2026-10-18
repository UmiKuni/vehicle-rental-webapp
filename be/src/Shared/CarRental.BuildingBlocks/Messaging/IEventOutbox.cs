namespace CarRental.BuildingBlocks.Messaging;

/// <summary>
/// Abstraction for publishing integration events through a transactional outbox.
/// </summary>
public interface IEventOutbox
{
    /// <summary>
    /// Enqueues an integration event into the outbox for transactional delivery.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;

    /// <summary>
    /// Enqueues a batch of integration events into the outbox for transactional delivery.
    /// </summary>
    Task PublishRangeAsync(IEnumerable<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default);
}
