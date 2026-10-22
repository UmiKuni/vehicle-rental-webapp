using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Data.Repositories;
using CarRental.Modules.Users.Endpoints;
using CarRental.Modules.Users.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Users;

public static class UsersModuleExtensions
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=carrental;Username=postgres;Password=postgres";

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema)));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUsersApi, UsersApiService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }

    public static IEndpointRouteBuilder UseUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapUsersEndpoints();
    }
}
