using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingCancelled"/>.
/// </summary>
[WolverineHandler]
public sealed class BookingCancelledConsumer
{
    public Task Handle(BookingCancelled message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IReportingRepository>(),
            services.GetRequiredService<ReportingDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    internal Task Handle(
        BookingCancelled message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleStatsAndProjectionAsync(
            message.EventId,
            message.BookingId,
            message.OccurredAt,
            repository,
            dbContext,
            clock,
            stats => stats.ApplyBookingCancelled(message.OccurredAt),
            projection => projection.MarkCancelled(message.OccurredAt),
            ct);
}
