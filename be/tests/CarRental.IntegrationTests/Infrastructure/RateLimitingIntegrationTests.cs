using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Users.Contracts.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace CarRental.IntegrationTests.Infrastructure;

public class RateLimitingIntegrationTests : IntegrationTestBase
{
    public RateLimitingIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task AuthLogin_ExceedingPermitLimit_ShouldReturn429WithRetryAfterAndErrorCode()
    {
        using var rateLimitedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:Enabled"] = "true",
                    ["RateLimiting:AuthLogin:PermitLimit"] = "2",
                    ["RateLimiting:AuthLogin:WindowSeconds"] = "60"
                });
            });
        });

        using var client = rateLimitedFactory.CreateClient();
        var payload = new LoginUserDto("rate_limit_user@example.com", "WrongPassword123!");

        var first = await client.PostAsJsonAsync("/api/auth/login", payload);
        first.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        first.Headers.GetValues("X-RateLimit-Limit").Should().ContainSingle("2");
        first.Headers.GetValues("X-RateLimit-Remaining").Should().ContainSingle("1");

        var second = await client.PostAsJsonAsync("/api/auth/login", payload);
        second.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        second.Headers.GetValues("X-RateLimit-Limit").Should().ContainSingle("2");
        second.Headers.GetValues("X-RateLimit-Remaining").Should().ContainSingle("0");

        var third = await client.PostAsJsonAsync("/api/auth/login", payload);
        third.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        third.Headers.TryGetValues("Retry-After", out var retryAfterValues).Should().BeTrue();
        int.Parse(retryAfterValues!.Single()).Should().BeGreaterThan(0);

        using var body = JsonDocument.Parse(await third.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errorCode").GetString().Should().Be("rate_limit.exceeded");
    }
}
