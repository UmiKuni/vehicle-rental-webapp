using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Data;
using CarRental.Modules.Fleet.Data.Repositories;
using CarRental.Modules.Fleet.Endpoints;
using CarRental.Modules.Fleet.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Fleet;

public static class FleetModuleExtensions
{
    public static IServiceCollection AddFleetModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=carrental;Username=postgres;Password=postgres";

        services.AddDbContext<FleetDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", FleetDbContext.Schema);
            }));

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IFleetApi, FleetApiService>();

        return services;
    }

    public static IEndpointRouteBuilder UseFleetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapFleetEndpoints();
    }
}
