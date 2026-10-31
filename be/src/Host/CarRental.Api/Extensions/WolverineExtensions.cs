using CarRental.BuildingBlocks.Messaging;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;

namespace CarRental.Api.Extensions;

public static class WolverineExtensions
{
    public const string WolverineSchema = "wolverine";

    /// <summary>
    /// Configures Wolverine as an in-process bus with a PostgreSQL-backed transactional outbox/inbox.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        builder.Host.UseWolverine(opts =>
        {
            opts.ServiceName = "CarRental.Api";

            // Durable outbox/inbox storage in PostgreSQL
            opts.PersistMessagesWithPostgresql(connectionString, WolverineSchema);

            // Discover handlers in all module assemblies (CarRental.Modules.*)
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.GetName().Name?.StartsWith("CarRental.Modules.") == true))
            {
                opts.Discovery.IncludeAssembly(assembly);
            }

            // Local queues are durable and use the outbox
            opts.Policies.UseDurableLocalQueues();
            opts.Policies.AutoApplyTransactions();

            // Retry policy for transient failures
            opts.OnException<Exception>()
                .RetryWithCooldown(
                    TimeSpan.FromMilliseconds(200),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(5));

            // Create outbox/inbox tables automatically
            opts.AutoBuildMessageStorageOnStartup = true;
        });

        builder.Services.AddScoped<IEventOutbox, WolverineEventOutbox>();

        return builder;
    }
}

/// <summary>
/// <see cref="IEventOutbox"/> implementation backed by Wolverine's message bus.
/// </summary>
internal sealed class WolverineEventOutbox(IMessageBus bus) : IEventOutbox
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await bus.PublishAsync(integrationEvent);
    }

    public async Task PublishRangeAsync(IEnumerable<IIntegrationEvent> integrationEvents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(integrationEvents);
        foreach (var integrationEvent in integrationEvents)
        {
            await bus.PublishAsync(integrationEvent);
        }
    }
}
