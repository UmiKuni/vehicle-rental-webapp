using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Fleet;

public static class FleetModule
{
    public static IServiceCollection AddFleetModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<FleetDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", FleetDbContext.Schema)));

        services.AddScoped<IFleetApi, FleetApiService>();

        return services;
    }
}
