using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using Wolverine.Attributes;

namespace CarRental.Modules.Reporting.EventHandlers;

[WolverineHandler]
public sealed class ReportingBookingLifecycleHandlers
{
    public Task Handle(
        BookingConfirmed message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p => p.MarkConfirmed(),
            ct);

    public Task Handle(
        BookingPaid message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p => p.MarkPaid(message.OccurredAt),
            ct);

    public Task Handle(
        BookingRejected message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p => p.MarkRejected(),
            ct);

    public Task Handle(
        BookingExpired message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p => p.MarkExpired(),
            ct);

    public Task Handle(
        BookingCompleted message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p => p.MarkCompleted(message.OccurredAt),
            ct);

    public Task Handle(
        HandoverConfirmed message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct = default) =>
        ReportingEventHelpers.HandleProjectionTransitionAsync(
            message.EventId,
            message.BookingId,
            repository,
            dbContext,
            clock,
            p =>
            {
                if (string.Equals(message.Type, nameof(HandoverType.Pickup), StringComparison.OrdinalIgnoreCase))
                    p.MarkInProgress();
            },
            ct);
}
