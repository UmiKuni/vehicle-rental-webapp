using CarRental.Modules.Pricing.Data;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Pricing;

public static class PricingModule
{
    public static IServiceCollection AddPricingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<PricingDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", PricingDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IPricingApi, PricingApiService>();

        return services;
    }
}
