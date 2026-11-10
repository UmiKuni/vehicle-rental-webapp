using System.Data.Common;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace CarRental.IntegrationTests.Fixtures;

public class CarRentalApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string TestJwtSigningKey = "integration-testing-super-secret-signing-key-32-chars-long";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:16-3.4-alpine")
        .WithDatabase("carrental_integration")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))
        .Build();

    private DbConnection _dbConnection = default!;
    private Respawner _respawner = default!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        // Trigger host build to execute migrations in Development environment
        using var client = CreateClient();

        _dbConnection = new NpgsqlConnection(_dbContainer.GetConnectionString());
        await _dbConnection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_dbConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude =
            [
                "users",
                "fleet",
                "search",
                "reviews",
                "support",
                "notifications",
                "audit",
                "bookings",
                "payments",
                "pricing",
                "availability",
                "handover",
                "reporting",
                "wolverine"
            ],
            TablesToIgnore =
            [
                new Respawn.Graph.Table("__EFMigrationsHistory")
            ]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["Authentication:Jwt:SigningKey"] = TestJwtSigningKey,
                ["Authentication:Jwt:Issuer"] = "CarRental.Api",
                ["Authentication:Jwt:Audience"] = "CarRental.Client",
                ["Authentication:Jwt:AccessTokenMinutes"] = "60",
                ["Authentication:Jwt:RefreshTokenDays"] = "30"
            });
        });
    }

    public async Task ResetDatabaseAsync()
    {
        if (_dbConnection is not null && _respawner is not null)
        {
            await _respawner.ResetAsync(_dbConnection);
        }
    }

    public new async Task DisposeAsync()
    {
        if (_dbConnection is not null)
        {
            await _dbConnection.DisposeAsync();
        }

        await _dbContainer.DisposeAsync();
    }
}
