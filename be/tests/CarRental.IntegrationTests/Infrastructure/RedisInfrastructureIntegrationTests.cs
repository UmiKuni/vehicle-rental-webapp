using CarRental.Api.Infrastructure.RateLimiting;
using CarRental.Api.Infrastructure.Redis;
using CarRental.BuildingBlocks.Auth;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Redis;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts.Dtos;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CarRental.IntegrationTests.Infrastructure;

public sealed class RedisInfrastructureIntegrationTests : IAsyncLifetime
{
    private readonly IContainer _redisContainer = new ContainerBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379))
        .Build();

    private ServiceProvider _serviceProvider = default!;

    public async Task InitializeAsync()
    {
        await _redisContainer.StartAsync();
        var endpoint = $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = endpoint,
                ["Redis:Enabled"] = "true",
                ["Redis:KeyPrefix"] = $"test:{Guid.NewGuid():N}:"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddRedisInfrastructure(configuration, new TestHostEnvironment());
        services.AddDistributedRateLimiting(configuration);

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();

        await _redisContainer.DisposeAsync();
    }

    [Fact]
    public async Task RedisDistributedLockProvider_ShouldEnforceMutualExclusion_TtlExpiry_AndLuaSafeRelease()
    {
        var provider = _serviceProvider.GetRequiredService<IDistributedLockProvider>();
        provider.Should().BeOfType<RedisDistributedLockProvider>();

        var key = $"lock:vehicle:{Guid.NewGuid():N}";

        var lockA = await provider.TryAcquireAsync(key, TimeSpan.FromMilliseconds(300), TimeSpan.Zero);
        lockA.Should().NotBeNull();

        var contended = await provider.TryAcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        contended.Should().BeNull();

        await Task.Delay(450);

        var lockB = await provider.TryAcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        lockB.Should().NotBeNull();

        // Disposing expired lockA must NOT delete lockB because the Lua script verifies token ownership.
        await lockA!.DisposeAsync();

        var stillHeld = await provider.TryAcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        stillHeld.Should().BeNull();

        await lockB!.DisposeAsync();

        await using var lockC = await provider.TryAcquireAsync(key, TimeSpan.FromSeconds(5), TimeSpan.Zero);
        lockC.Should().NotBeNull();
    }

    [Fact]
    public async Task RedisTokenRevocationStore_ShouldRoundTripJtiAndValidAfterWithTtl()
    {
        var store = _serviceProvider.GetRequiredService<ITokenRevocationStore>();
        store.Should().BeOfType<RedisTokenRevocationStore>();

        var jti = Guid.NewGuid().ToString("N");
        (await store.IsJtiRevokedAsync(jti)).Should().BeFalse();

        await store.RevokeJtiAsync(jti, TimeSpan.FromMilliseconds(300));
        (await store.IsJtiRevokedAsync(jti)).Should().BeTrue();

        await Task.Delay(450);
        (await store.IsJtiRevokedAsync(jti)).Should().BeFalse();

        var userId = Guid.NewGuid();
        var validAfter = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await store.SetTokensValidAfterAsync(userId, validAfter, TimeSpan.FromSeconds(30));

        var loaded = await store.GetTokensValidAfterAsync(userId);
        loaded.Should().Be(validAfter);
    }

    [Fact]
    public async Task RedisCacheService_ShouldRoundTripJsonDtos_Ttl_AndVersionCounter()
    {
        var cache = _serviceProvider.GetRequiredService<ICacheService>();
        cache.Should().BeOfType<RedisCacheService>();

        var vehicleId = Guid.NewGuid();
        var cleaningFee = Money.Of(100_000);
        var plan = new PricingPlanDto(
            vehicleId,
            Money.Of(150_000),
            Money.Of(900_000),
            Money.Of(1_100_000),
            Money.Of(3_000_000),
            300,
            [new FeeRuleDto("Cleaning", cleaningFee)]);

        await cache.SetAsync($"pricing:plan:{vehicleId:D}", plan, TimeSpan.FromSeconds(10));
        var cachedPlan = await cache.GetAsync<PricingPlanDto>($"pricing:plan:{vehicleId:D}");
        cachedPlan.Should().NotBeNull();
        cachedPlan!.WeekdayRate.Should().Be(Money.Of(900_000));
        cachedPlan.FeeRules.Should().ContainSingle(r => r.Type == "Cleaning" && r.Amount == cleaningFee);

        var summary = new VehicleSummaryDto(
            vehicleId,
            Guid.NewGuid(),
            "Published",
            Guid.NewGuid(),
            "Toyota",
            Guid.NewGuid(),
            "Camry",
            "51A-12345",
            "Auto",
            5,
            "vehicles/camry.jpg",
            GeoPoint.Create(10.7769, 106.7009).Value,
            "District 1, HCMC");

        await cache.SetAsync($"fleet:vehicle:summary:{vehicleId:D}", summary, TimeSpan.FromMilliseconds(300));
        var cachedSummary = await cache.GetAsync<VehicleSummaryDto>($"fleet:vehicle:summary:{vehicleId:D}");
        cachedSummary.Should().NotBeNull();
        cachedSummary!.Location.Latitude.Should().Be(10.7769);
        cachedSummary.Location.Longitude.Should().Be(106.7009);

        await Task.Delay(450);
        (await cache.GetAsync<VehicleSummaryDto>($"fleet:vehicle:summary:{vehicleId:D}")).Should().BeNull();

        await cache.RemoveAsync($"pricing:plan:{vehicleId:D}");
        (await cache.GetAsync<PricingPlanDto>($"pricing:plan:{vehicleId:D}")).Should().BeNull();

        const string versionKey = "search:version";
        (await cache.GetVersionAsync(versionKey)).Should().Be(0);
        (await cache.IncrementVersionAsync(versionKey)).Should().Be(1);
        (await cache.GetVersionAsync(versionKey)).Should().Be(1);
    }

    [Fact]
    public async Task RedisRateLimiter_ShouldEnforceFixedWindowLimitAndReset()
    {
        var limiter = _serviceProvider.GetRequiredService<IRateLimiter>();
        limiter.Should().BeOfType<RedisRateLimiter>();

        var partition = $"ip:127.0.0.1:{Guid.NewGuid():N}";
        var window = TimeSpan.FromMilliseconds(500);

        var d1 = await limiter.CheckAsync("auth-login", partition, 2, window);
        d1.IsAllowed.Should().BeTrue();
        d1.Remaining.Should().Be(1);

        var d2 = await limiter.CheckAsync("auth-login", partition, 2, window);
        d2.IsAllowed.Should().BeTrue();
        d2.Remaining.Should().Be(0);

        var d3 = await limiter.CheckAsync("auth-login", partition, 2, window);
        d3.IsAllowed.Should().BeFalse();
        d3.Remaining.Should().Be(0);
        d3.RetryAfterSeconds.Should().BeGreaterThanOrEqualTo(1);

        await Task.Delay(650);

        var d4 = await limiter.CheckAsync("auth-login", partition, 2, window);
        d4.IsAllowed.Should().BeTrue();
        d4.Remaining.Should().Be(1);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "CarRental.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
