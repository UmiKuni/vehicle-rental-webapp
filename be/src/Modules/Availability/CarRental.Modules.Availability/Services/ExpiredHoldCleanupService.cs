using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Availability.Services;

internal sealed class ExpiredHoldCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiredHoldCleanupService> _logger;

    public ExpiredHoldCleanupService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<ExpiredHoldCleanupService> logger)
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
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IVehicleCalendarRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AvailabilityDbContext>();

        var now = _clock.GetUtcNow();
        var calendars = await repository.GetCalendarsWithExpiredHoldsAsync(now, ct);

        var totalExpired = 0;
        foreach (var calendar in calendars)
        {
            totalExpired += calendar.ExpireHolds(now);
        }

        if (totalExpired > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Cleaned up {ExpiredHoldCount} expired availability hold(s).", totalExpired);
        }

        return totalExpired;
    }
}
