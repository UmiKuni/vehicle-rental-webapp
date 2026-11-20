using CarRental.BuildingBlocks.Storage;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Handover.Contracts;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Handover.Data.Repositories;
using CarRental.Modules.Handover.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Handover;

public static class HandoverModule
{
    public static IServiceCollection AddHandoverModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<HandoverDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", HandoverDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IImageStorage, InMemoryImageStorage>();
        services.AddValidatorsFromAssembly(typeof(HandoverModule).Assembly);
        services.AddScoped<IHandoverRepository, HandoverRepository>();
        services.AddScoped<IHandoverApi, HandoverApiService>();

        return services;
    }
}
