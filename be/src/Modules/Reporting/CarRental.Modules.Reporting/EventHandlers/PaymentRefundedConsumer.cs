using CarRental.BuildingBlocks.Redis;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentRefunded"/>.
/// </summary>
[WolverineHandler]
public sealed class PaymentRefundedConsumer
{
    public Task Handle(PaymentRefunded message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IReportingRepository>(),
            services.GetRequiredService<ReportingDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct,
            services.GetService<ICacheService>());

    internal Task Handle(
        PaymentRefunded message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default,
        ICacheService? cacheService = null) =>
        ReportingEventHelpers.HandleStatsAndProjectionAsync(
            message.EventId,
            message.BookingId,
            message.OccurredAt,
            repository,
            dbContext,
            clock,
            stats => stats.ApplyPaymentRefunded(message.RefundedAmount.Amount, message.OccurredAt),
            projection => projection.ApplyRefund(message.RefundedAmount.Amount),
            ct,
            cacheService);
}
