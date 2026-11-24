using CarRental.BuildingBlocks.Redis;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Availability.Services;

internal sealed class ExpiredHoldCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LeaderLockTtl = TimeSpan.FromSeconds(55);
    private static readonly TimeSpan VehicleLockTtl = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan VehicleLockWait = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiredHoldCleanupService> _logger;
    private readonly IDistributedLockProvider _lockProvider;

    public ExpiredHoldCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<ExpiredHoldCleanupService> logger,
        IDistributedLockProvider? lockProvider = null)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
        _lockProvider = lockProvider ?? new InMemoryDistributedLockProvider();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, _clock, stoppingToken);
                await CleanupExpiredHoldsOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while cleaning up expired availability holds.");
            }
        }
    }

    public async Task<int> CleanupExpiredHoldsOnceAsync(CancellationToken ct = default)
    {
        await using var leaderLock = await _lockProvider.TryAcquireAsync(
            "lock:job:expired-holds",
            LeaderLockTtl,
            TimeSpan.Zero,
            ct);

        if (leaderLock is null)
            return 0;

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleCalendarRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();

        var now = _clock.GetUtcNow();
        var calendars = await repository.GetCalendarsWithExpiredHoldsAsync(now, ct);

        var totalExpired = 0;
        foreach (var calendar in calendars)
        {
            await using var vehicleLock = await _lockProvider.TryAcquireAsync(
                $"lock:availability:vehicle:{calendar.VehicleId:N}",
                VehicleLockTtl,
                VehicleLockWait,
                ct);

            if (vehicleLock is null)
                continue;

            var expiredForCalendar = calendar.ExpireHolds(now);
            if (expiredForCalendar <= 0)
                continue;

            try
            {
                await dbContext.SaveChangesAsync(ct);
                totalExpired += expiredForCalendar;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Skipping expired hold cleanup for Vehicle {VehicleId} due to concurrent modification.", calendar.VehicleId);
                repository.ClearChangeTracker();
            }
        }

        if (totalExpired > 0)
        {
            _logger.LogInformation("Cleaned up {ExpiredHoldCount} expired availability hold(s).", totalExpired);
        }

        return totalExpired;
    }
}
