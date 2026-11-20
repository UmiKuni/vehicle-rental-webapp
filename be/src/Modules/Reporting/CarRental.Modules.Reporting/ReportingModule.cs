using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Reporting.Contracts;
using CarRental.Modules.Reporting.Data;
using CarRental.Modules.Reporting.Data.Repositories;
using CarRental.Modules.Reporting.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", ReportingDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(ReportingModule).Assembly);
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.AddScoped<IReportingQueries, ReportingQueryService>();

        return services;
    }
}
