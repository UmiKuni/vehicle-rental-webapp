using CarRental.BuildingBlocks.Messaging;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Data.Repositories;
using CarRental.Modules.Bookings.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Bookings.Services;

public sealed class BookingExpirationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PendingHoldDuration = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<BookingExpirationService> _logger;

    public BookingExpirationService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<BookingExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, _clock, stoppingToken);
                await ExpireOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while expiring overdue bookings.");
            }
        }
    }

    public async Task<int> ExpireOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        var availabilityApi = scope.ServiceProvider.GetRequiredService<IAvailabilityApi>();
        var eventOutbox = scope.ServiceProvider.GetRequiredService<IEventOutbox>();

        var now = _clock.GetUtcNow();
        var holdCutoff = now - PendingHoldDuration;

        var pendingIds = await repository.GetExpiredPendingIdsAsync(holdCutoff, ct);
        var confirmedIds = await repository.GetExpiredConfirmedIdsAsync(now, ct);

        var expired = new List<(Booking Booking, string Reason)>();

        foreach (var id in pendingIds)
        {
            var booking = await repository.GetByIdAsync(id, ct);
            if (booking is null)
                continue;

            const string reason = "Owner approval hold window (12h) expired.";
            if (booking.Expire(reason, now).IsSuccess)
                expired.Add((booking, reason));
        }

        foreach (var id in confirmedIds)
        {
            var booking = await repository.GetByIdAsync(id, ct);
            if (booking is null)
                continue;

            const string reason = "Payment deadline expired.";
            if (booking.Expire(reason, now).IsSuccess)
                expired.Add((booking, reason));
        }

        if (expired.Count == 0)
            return 0;

        await dbContext.SaveChangesAsync(ct);

        foreach (var (booking, reason) in expired)
        {
            await availabilityApi.ReleaseHoldAsync(booking.VehicleId, booking.Id.Value, ct);
            await eventOutbox.PublishAsync(
                new BookingExpired(Guid.CreateVersion7(), now, booking.Id.Value, reason),
                ct);
        }

        _logger.LogInformation("Expired {ExpiredBookingCount} overdue booking(s).", expired.Count);
        return expired.Count;
    }
}
