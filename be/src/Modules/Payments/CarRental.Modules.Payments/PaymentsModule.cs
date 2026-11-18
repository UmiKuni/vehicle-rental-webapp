using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", PaymentsDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddValidatorsFromAssembly(typeof(PaymentsModule).Assembly);
        services.AddScoped<IPaymentsRepository, PaymentsRepository>();
        services.AddScoped<IPaymentGateway, MockPaymentGateway>();
        services.AddScoped<IPaymentsApi, PaymentsApiService>();
        services.AddHostedService<DepositAutoReleaseService>();

        return services;
    }
}
