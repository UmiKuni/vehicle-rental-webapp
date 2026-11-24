using CarRental.BuildingBlocks.Redis;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Data.Repositories;
using CarRental.Modules.Bookings.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CarRental.Modules.Bookings;

public static class BookingsModule
{
    public static IServiceCollection AddBookingsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<BookingDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", BookingDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IDistributedLockProvider, InMemoryDistributedLockProvider>();
        services.AddValidatorsFromAssembly(typeof(BookingsModule).Assembly);
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingApi, BookingApiService>();
        services.AddHostedService<BookingExpirationService>();

        return services;
    }
}
