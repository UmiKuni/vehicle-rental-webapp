using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Contracts;
using CarRental.Modules.Search.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Search;

public static class SearchModule
{
    public static IServiceCollection AddSearchModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<SearchDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
            {
                o.MigrationsHistoryTable("__EFMigrationsHistory", SearchDbContext.Schema);
                o.UseNetTopologySuite();
            }));

        services.AddScoped<ISearchApi, SearchApiService>();

        return services;
    }
}
