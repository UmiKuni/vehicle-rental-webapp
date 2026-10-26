using CarRental.Modules.Availability.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
