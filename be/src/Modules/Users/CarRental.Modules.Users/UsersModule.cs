using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.Modules.Users;

public static class UsersModule
{
    public static IServiceCollection AddUsersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<UsersDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", UsersDbContext.Schema)));

        // Register domain services
        services.AddScoped<IUsersApi, UsersApiService>();
        services.AddScoped<PasswordHasher<object>>();

        return services;
    }
}
