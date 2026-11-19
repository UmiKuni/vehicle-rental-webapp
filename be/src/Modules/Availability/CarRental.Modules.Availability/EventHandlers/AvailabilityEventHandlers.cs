using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Bookings.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Availability.EventHandlers;

[WolverineHandler]
public sealed class AvailabilityEventHandlers
{
    public Task Handle(BookingCancelled message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IVehicleCalendarRepository>(),
            services.GetRequiredService<AvailabilityDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    public Task Handle(BookingRejected message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IVehicleCalendarRepository>(),
            services.GetRequiredService<AvailabilityDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    public Task Handle(BookingExpired message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IVehicleCalendarRepository>(),
            services.GetRequiredService<AvailabilityDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    internal Task Handle(
        BookingCancelled message,
        IVehicleCalendarRepository repository,
        AvailabilityDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct) =>
        ReleaseAsync(message.BookingId, repository, dbContext, clock, ct);

    internal Task Handle(
        BookingRejected message,
        IVehicleCalendarRepository repository,
        AvailabilityDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct) =>
        ReleaseAsync(message.BookingId, repository, dbContext, clock, ct);

    internal Task Handle(
        BookingExpired message,
        IVehicleCalendarRepository repository,
        AvailabilityDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct) =>
        ReleaseAsync(message.BookingId, repository, dbContext, clock, ct);

    private static async Task ReleaseAsync(
        Guid bookingId,
        IVehicleCalendarRepository repository,
        AvailabilityDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (bookingId == Guid.Empty)
            return;

        var calendar = await repository.GetByBookingIdAsync(bookingId, ct);
        if (calendar is null)
            return;

        var result = calendar.Release(bookingId, clock.GetUtcNow());
        if (result.IsSuccess)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
