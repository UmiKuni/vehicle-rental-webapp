using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarRental.Api.Infrastructure.OpenApi;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CarRental.IntegrationTests.Infrastructure;

public class OpenApiConfigurationTests
{
    [Fact]
    public async Task OpenApiDocument_IncludesBearerSecurityAndRequestBodyExamples()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        builder.Services.AddCarRentalOpenApi();

        await using var app = builder.Build();
        app.MapOpenApi();
        app.MapPost("/api/users/login", (LoginUserDto dto) => Results.Ok());
        app.MapGet("/api/users/me", () => Results.Ok()).RequireAuthorization();
        app.MapPost("/api/test/bookings", (CreateBookingRequest req) => Results.Ok()).RequireAuthorization();

        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/v1.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var servers = root.GetProperty("servers").EnumerateArray()
            .Select(s => s.GetProperty("url").GetString())
            .ToArray();
        servers.Should().Contain("http://localhost:8080");
        servers.Should().Contain("/");

        var bearerScheme = root.GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        bearerScheme.GetProperty("type").GetString().Should().Be("http");
        bearerScheme.GetProperty("scheme").GetString().Should().Be("bearer");
        bearerScheme.GetProperty("bearerFormat").GetString().Should().Be("JWT");

        var paths = root.GetProperty("paths");

        var loginPost = paths.GetProperty("/api/users/login").GetProperty("post");
        loginPost.TryGetProperty("security", out _).Should().BeFalse();
        var loginExample = loginPost.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");
        loginExample.GetProperty("identifier").GetString().Should().Be("customer.seed@example.com");
        loginExample.GetProperty("password").GetString().Should().Be("Password123!");

        var meGet = paths.GetProperty("/api/users/me").GetProperty("get");
        var meSecurity = meGet.GetProperty("security").EnumerateArray().ToList();
        meSecurity.Should().ContainSingle().Which.TryGetProperty("Bearer", out _).Should().BeTrue();

        var bookingsPost = paths.GetProperty("/api/test/bookings").GetProperty("post");
        var bookingsSecurity = bookingsPost.GetProperty("security").EnumerateArray().ToList();
        bookingsSecurity.Should().ContainSingle().Which.TryGetProperty("Bearer", out _).Should().BeTrue();
        var bookingExample = bookingsPost.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("example");
        bookingExample.ValueKind.Should().Be(JsonValueKind.Object);
        bookingExample.EnumerateObject().Should().NotBeEmpty();

        await app.StopAsync();
    }
}
