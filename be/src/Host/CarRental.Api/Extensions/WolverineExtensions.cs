using CarRental.BuildingBlocks.Messaging;
using CarRental.Modules.Audit.Contracts.Commands;
using CarRental.Modules.Audit.EventHandlers;
using CarRental.Modules.Availability.EventHandlers;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Payments.EventHandlers;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Reporting.EventHandlers;
using CarRental.Modules.Search.EventHandlers;
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
            opts.Discovery.IncludeType<AuditCommandHandlers>();
            opts.Discovery.IncludeType<SearchEventHandlers>();
            opts.Discovery.IncludeType<AvailabilityEventHandlers>();
            opts.Discovery.IncludeType<PaymentsEventHandlers>();
            opts.Discovery.IncludeType<BookingRequestedConsumer>();
            opts.Discovery.IncludeType<BookingCancelledConsumer>();
            opts.Discovery.IncludeType<PaymentSucceededConsumer>();
            opts.Discovery.IncludeType<PaymentRefundedConsumer>();
            opts.Discovery.IncludeType<ReportingBookingLifecycleHandlers>();
            opts.LocalQueue("search-events");
            opts.PublishMessage<VehiclePublished>().ToLocalQueue("search-events");
            opts.PublishMessage<PricingUpdated>().ToLocalQueue("search-events");
            opts.LocalQueue("availability-events");
            opts.PublishMessage<BookingCancelled>().ToLocalQueue("availability-events");
            opts.PublishMessage<BookingRejected>().ToLocalQueue("availability-events");
            opts.PublishMessage<BookingExpired>().ToLocalQueue("availability-events");
            opts.LocalQueue("payments-events");
            opts.PublishMessage<BookingConfirmed>().ToLocalQueue("payments-events");
            opts.PublishMessage<HandoverConfirmed>().ToLocalQueue("payments-events");
            opts.PublishMessage<PaymentSucceeded>().ToLocalQueue("payments-events");
            opts.PublishMessage<PaymentFailed>().ToLocalQueue("payments-events");
            opts.LocalQueue("reporting-events");
            opts.PublishMessage<BookingRequested>().ToLocalQueue("reporting-events");
            opts.PublishMessage<BookingPaid>().ToLocalQueue("reporting-events");
            opts.PublishMessage<BookingCompleted>().ToLocalQueue("reporting-events");
            opts.PublishMessage<PaymentRefunded>().ToLocalQueue("reporting-events");
            opts.LocalQueue("audit-events");
            opts.PublishMessage<RecordAuditEntry>().ToLocalQueue("audit-events");

            // Integration tests should not leave durable local queue work to drain after the
            // test database is torn down. Production retains durable local queues.
            var useDurableLocalQueues = builder.Configuration
                .GetValue("Wolverine:UseDurableLocalQueues", !builder.Environment.IsEnvironment("Testing"));
            if (useDurableLocalQueues)
            {
                opts.Policies.UseDurableLocalQueues();
            }
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
//
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
