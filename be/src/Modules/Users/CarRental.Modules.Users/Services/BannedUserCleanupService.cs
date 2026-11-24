using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CarRental.Modules.Users.Services
{
    public class BannedUserCleanupService : BackgroundService
    {
        private readonly ILogger<BannedUserCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _clock;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public BannedUserCleanupService(
            ILogger<BannedUserCleanupService> logger,
            IServiceScopeFactory scopeFactory,
            TimeProvider clock)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _clock = clock;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BannedUserCleanupService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_interval, _clock, stoppingToken);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
                    // Placeholder logic: fetch banned users older than 30 days and log.
                    var cutoff = _clock.GetUtcNow().AddDays(-30);
                    var oldBanned = await db.Users
                        .Where(u => u.Status == UserStatus.Banned)
                        .ToListAsync(stoppingToken);
                    foreach (var user in oldBanned)
                    {
                        _logger.LogInformation("Found old banned user {UserId} for potential cleanup.", user.Id);
                        // Real cleanup could delete or archive; omitted for safety.
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during BannedUserCleanupService execution.");
                }
            }
            _logger.LogInformation("BannedUserCleanupService stopping.");
        }
    }
}
