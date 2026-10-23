using CarRental.Modules.Users.Contracts.Enums;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarRental.Modules.Users.Services
{
    public class BannedUserCleanupService : BackgroundService
    {
        private readonly ILogger<BannedUserCleanupService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public BannedUserCleanupService(ILogger<BannedUserCleanupService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BannedUserCleanupService started.");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Placeholder logic: fetch banned users older than 30 days and log.
                    var cutoff = DateTimeOffset.UtcNow.AddDays(-30);
                    var oldBanned = await db.Users
                        .Where(u => u.Status == UserStatus.Banned)
                        .ToListAsync(stoppingToken);
                    foreach (var user in oldBanned)
                    {
                        _logger.LogInformation("Found old banned user {UserId} for potential cleanup.", user.Id);
                        // Real cleanup could delete or archive; omitted for safety.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during BannedUserCleanupService execution.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
            _logger.LogInformation("BannedUserCleanupService stopping.");
        }
    }
}
