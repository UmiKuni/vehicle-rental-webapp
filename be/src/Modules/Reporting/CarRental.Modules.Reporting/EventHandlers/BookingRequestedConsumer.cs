using CarRental.BuildingBlocks.Redis;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Entities;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Reporting.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="BookingRequested"/>.
/// </summary>
[WolverineHandler]
public sealed class BookingRequestedConsumer
{
    public Task Handle(BookingRequested message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IReportingRepository>(),
            services.GetRequiredService<ReportingDbContext>(),
            services.GetRequiredService<IFleetApi>(),
            services.GetRequiredService<TimeProvider>(),
            ct,
            services.GetService<ICacheService>());

    internal async Task Handle(
        BookingRequested message,
        IReportingRepository repository,
        ReportingDbContext dbContext,
        IFleetApi fleetApi,
        TimeProvider clock,
        CancellationToken ct = default,
        ICacheService? cacheService = null)
    {
        if (message.EventId == Guid.Empty)
            return;

        if (await repository.HasEventBeenProcessedAsync(message.EventId, ct))
            return;

        var (plate, model) = await ResolveVehicleMetadataAsync(message.VehicleId, fleetApi, ct);

        await ReportingEventHelpers.ProjectionLock.WaitAsync(ct);
        try
        {
            if (await repository.HasEventBeenProcessedAsync(message.EventId, ct))
                return;

            var stats = await ReportingEventHelpers.GetOrCreateDailyStatsAsync(message.OccurredAt, repository, ct);
            stats.ApplyBookingRequested(message.OccurredAt);

            var projection = await repository.GetBookingProjectionAsync(message.BookingId, ct);
            if (projection is null)
            {
                projection = BookingReportProjection.Create(
                    bookingId: message.BookingId,
                    ownerId: message.OwnerId,
                    vehicleId: message.VehicleId,
                    vehiclePlate: plate,
                    vehicleModel: model,
                    startTime: message.Period.Start,
                    endTime: message.Period.End,
                    rentAmount: message.Total.Amount,
                    createdAt: message.OccurredAt);
                repository.AddBookingProjection(projection);
            }

            repository.MarkEventProcessed(ProcessedEvent.Create(message.EventId, clock.GetUtcNow()));
            await dbContext.SaveChangesAsync(ct);
            await ReportingEventHelpers.InvalidateReportingCacheAsync(cacheService, clock, message.OccurredAt, projection, ct);
        }
        finally
        {
            ReportingEventHelpers.ProjectionLock.Release();
        }
    }

    private static async Task<(string Plate, string Model)> ResolveVehicleMetadataAsync(
        Guid vehicleId,
        IFleetApi fleetApi,
        CancellationToken ct)
    {
        if (vehicleId == Guid.Empty)
            return (string.Empty, string.Empty);

        try
        {
            var result = await fleetApi.GetVehicleSummaryAsync(vehicleId, ct);
            if (!result.IsSuccess)
                return (string.Empty, string.Empty);

            var v = result.Value;
            var model = string.IsNullOrWhiteSpace(v.BrandName)
                ? v.ModelName
                : $"{v.BrandName} {v.ModelName}".Trim();

            return (v.PlateNumber, model);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return (string.Empty, string.Empty);
        }
    }
}
