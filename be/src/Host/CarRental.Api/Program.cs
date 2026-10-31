using CarRental.Api.Extensions;
using CarRental.Modules.Audit;
using CarRental.Modules.Availability;
using CarRental.Modules.Availability.Endpoints;
using CarRental.Modules.Bookings;
using CarRental.Modules.Bookings.Endpoints;
using CarRental.Modules.Fleet;
using CarRental.Modules.Handover;
using CarRental.Modules.Handover.Endpoints;
using CarRental.Modules.Notifications;
using CarRental.Modules.Payments;
using CarRental.Modules.Payments.Endpoints;
using CarRental.Modules.Pricing;
using CarRental.Modules.Pricing.Endpoints;
using CarRental.Modules.Reporting;
using CarRental.Modules.Reporting.Endpoints;
using CarRental.Modules.Reviews;
using CarRental.Modules.Search;
using CarRental.Modules.Support;
using CarRental.Modules.Users;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Messaging: Wolverine with PostgreSQL transactional outbox
builder.AddMessaging();

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

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
        {
            new() { Url = "http://localhost:8080", Description = "Local Docker API" },
            new() { Url = "/", Description = "Relative Origin" }
        };
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Auto-migrate all 13 module schemas on development startup
    await app.ApplyMigrationsAsync();

    app.MapOpenApi();
    app.MapScalarApiReference(); //http://localhost:8080/scalar/v1
}

// Map Module Endpoints (Mock Implementation)
app.MapPricingEndpoints();
app.MapAvailabilityEndpoints();
app.MapBookingsEndpoints();
app.MapPaymentsEndpoints();
app.MapHandoverEndpoints();
app.MapReportingEndpoints();

app.Run();

