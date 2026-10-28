using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Data;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text;

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
        services.AddScoped<ITokenService, TokenService>();
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.Issuer) &&
                !string.IsNullOrWhiteSpace(options.Audience) &&
                options.SigningKey.Length >= 32 &&
                options.AccessTokenMinutes > 0 &&
                options.RefreshTokenDays > 0,
                "JWT configuration is incomplete or invalid.")
            .ValidateOnStart();

        return services;
    }

    public static IServiceCollection AddUsersAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
            throw new InvalidOperationException("JWT signing key must be at least 32 characters.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (!Guid.TryParse(userIdClaim, out var userId))
                        {
                            context.Fail("The token does not contain a valid user identifier.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<UsersDbContext>();
                        var user = await db.Users.AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == UserId.From(userId), context.HttpContext.RequestAborted);
                        if (user is null || user.Status == Contracts.Enums.UserStatus.Banned)
                            context.Fail("The user account is unavailable.");
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser())
            .AddPolicy("CustomerOnly", policy => policy.RequireRole(Contracts.Enums.UserRole.Customer.ToString()))
            .AddPolicy("OwnerOnly", policy => policy.RequireRole(Contracts.Enums.UserRole.Owner.ToString()))
            .AddPolicy("AdminOnly", policy => policy.RequireRole(Contracts.Enums.UserRole.Admin.ToString()));

        return services;
    }
}
