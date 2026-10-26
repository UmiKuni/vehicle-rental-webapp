using CarRental.Modules.Audit.Data;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Notifications.Data;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reviews.Data;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Support.Data;
using CarRental.Modules.Users.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Api.Extensions;

public static class MigrationExtensions
{
    public static async Task ApplyMigrationsAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigration");

        var contexts = new DbContext[]
        {
            services.GetRequiredService<UsersDbContext>(),
            services.GetRequiredService<FleetDbContext>(),
            services.GetRequiredService<PricingDbContext>(),
            services.GetRequiredService<AvailabilityDbContext>(),
            services.GetRequiredService<BookingDbContext>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetRequiredService<HandoverDbContext>(),
            services.GetRequiredService<ReviewsDbContext>(),
            services.GetRequiredService<SupportDbContext>(),
            services.GetRequiredService<SearchDbContext>(),
            services.GetRequiredService<NotificationsDbContext>(),
            services.GetRequiredService<AuditDbContext>(),
            services.GetRequiredService<ReportingDbContext>()
        };

        foreach (var context in contexts)
        {
            var contextName = context.GetType().Name;
            try
            {
                logger.LogInformation("Applying migrations for {ContextName}...", contextName);
                await context.Database.MigrateAsync();
                logger.LogInformation("Successfully applied migrations for {ContextName}.", contextName);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply migrations for {ContextName}.", contextName);
                throw;
            }
        }
    }
}
