using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Payments.Services;

public sealed class DepositAutoReleaseService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<DepositAutoReleaseService> _logger;

    public DepositAutoReleaseService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<DepositAutoReleaseService> logger)
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
                await ReleaseDueDepositsOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while auto-releasing due security deposits.");
            }
        }
    }

    public async Task<int> ReleaseDueDepositsOnceAsync(CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentsRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        var now = _clock.GetUtcNow();
        var dueDeposits = await repository.GetDueDepositsForReleaseAsync(now, ct);

        var releasedCount = 0;
        foreach (var deposit in dueDeposits)
        {
            var releaseResult = deposit.Release(now);
            if (releaseResult.IsSuccess)
                releasedCount++;
        }

        if (releasedCount > 0)
        {
            await dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Auto-released {ReleasedDepositCount} due security deposit(s).", releasedCount);
        }

        return releasedCount;
    }
}
