using CarRental.Api.Extensions;
using CarRental.Api.Infrastructure.ExceptionHandling;
using CarRental.Api.Infrastructure.OpenApi;
using CarRental.Api.Infrastructure.RateLimiting;
using CarRental.Api.Infrastructure.Redis;
using CarRental.Api.Infrastructure.Storage;
using CarRental.Modules.Audit;
using CarRental.Modules.Audit.Endpoints;
using CarRental.Modules.Availability;
using CarRental.Modules.Availability.Endpoints;
using CarRental.Modules.Bookings;
using CarRental.Modules.Bookings.Endpoints;
using CarRental.Modules.Fleet;
using CarRental.Modules.Fleet.Endpoints;
using CarRental.Modules.Handover;
using CarRental.Modules.Handover.Endpoints;
using CarRental.Modules.Notifications;
using CarRental.Modules.Notifications.Hubs;
using CarRental.Modules.Payments;
using CarRental.Modules.Payments.Endpoints;
using CarRental.Modules.Pricing;
using CarRental.Modules.Pricing.Endpoints;
using CarRental.Modules.Reporting;
using CarRental.Modules.Reporting.Endpoints;
using CarRental.Modules.Reviews;
using CarRental.Modules.Reviews.Endpoints;
using CarRental.Modules.Search;
using CarRental.Modules.Search.Endpoints;
using CarRental.Modules.Support;
using CarRental.Modules.Support.Endpoints;
using CarRental.Modules.Users;
using CarRental.Modules.Users.Endpoints;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddImageStorage(builder.Configuration, builder.Environment);
builder.Services.AddRedisInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddDistributedRateLimiting(builder.Configuration);

// Register modular monolith modules
builder.Services
    .AddUsersModule(builder.Configuration)
    .AddFleetModule(builder.Configuration)
    .AddPricingModule(builder.Configuration)
    .AddAvailabilityModule(builder.Configuration)
    .AddBookingsModule(builder.Configuration)
    .AddPaymentsModule(builder.Configuration)
    .AddHandoverModule(builder.Configuration)
    .AddReviewsModule(builder.Configuration)
    .AddSupportModule(builder.Configuration)
    .AddSearchModule(builder.Configuration)
    .AddNotificationsModule(builder.Configuration)
    .AddAuditModule(builder.Configuration)
    .AddReportingModule(builder.Configuration);

// Messaging: Wolverine with PostgreSQL transactional outbox.
// Register modules first so their handler assemblies are loaded for discovery.
builder.AddMessaging();

builder.Services.AddUsersAuthentication(builder.Configuration);

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<RouteHandlerOptions>(options =>
{
    options.ThrowOnBadRequest = true;
});

builder.Services.AddCarRentalOpenApi();

var app = builder.Build();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (app.Environment.IsDevelopment() && allowedOrigins.Length == 0)
{
    app.UseCors();
}
else
{
    app.UseCors(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
}

app.UseMiddleware<ApiExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RateLimitingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    await app.ApplyMigrationsAsync();

    if (app.Environment.IsDevelopment())
    {
        await app.SeedAsync();

        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle("CarRental API")
                .WithPreferredScheme("Bearer")
                .WithHttpBearerAuthentication(bearer =>
                {
                    bearer.Token = string.Empty;
                });
        }); //http://localhost:8080/scalar/v1
    }
}

// Map Module Endpoints
app.MapPricingEndpoints();
app.MapUsersEndpoints();
app.MapFleetEndpoints();
app.MapSearchEndpoints();
app.MapAvailabilityEndpoints();
app.MapBookingsEndpoints();
app.MapPaymentsEndpoints();
app.MapHandoverEndpoints();
app.MapReviewsEndpoints();
app.MapSupportEndpoints();
app.MapReportingEndpoints();
app.MapAuditEndpoints();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();

public partial class Program
{
}
