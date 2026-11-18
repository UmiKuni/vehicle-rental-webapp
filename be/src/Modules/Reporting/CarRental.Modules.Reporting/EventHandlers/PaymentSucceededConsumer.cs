using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using Wolverine.Attributes;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="PaymentSucceeded"/>.
/// </summary>
[WolverineHandler]
public sealed class PaymentSucceededConsumer
{
    public Task Handle(
        PaymentSucceeded message,
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
            stats => stats.ApplyPaymentSucceeded(message.Amount.Amount, message.OccurredAt),
            projection => projection.RecordPayment(message.Amount.Amount, message.OccurredAt),
            ct);
}
