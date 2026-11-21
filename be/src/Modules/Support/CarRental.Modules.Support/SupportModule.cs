using CarRental.Modules.Support.Data;
using CarRental.Modules.Support.Contracts;
using CarRental.Modules.Support.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Support;

public static class SupportModule
{
    public static IServiceCollection AddSupportModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<SupportDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", SupportDbContext.Schema)));

        services.AddScoped<ISupportApi, SupportApiService>();
        return services;
    }
}
