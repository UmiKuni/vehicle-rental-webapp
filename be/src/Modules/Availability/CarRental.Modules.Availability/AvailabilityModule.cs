using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Availability.Data;
using CarRental.Modules.Availability.Data.Repositories;
using CarRental.Modules.Availability.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Availability;

public static class AvailabilityModule
{
    public static IServiceCollection AddAvailabilityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<AvailabilityDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
            {
                o.MigrationsHistoryTable("__EFMigrationsHistory", AvailabilityDbContext.Schema);
                o.UseNetTopologySuite();
            }));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IVehicleCalendarRepository, VehicleCalendarRepository>();
        services.AddScoped<IAvailabilityApi, AvailabilityApiService>();
        services.AddHostedService<ExpiredHoldCleanupService>();

        return services;
    }
}
