using CarRental.BuildingBlocks.Auth;
using CarRental.BuildingBlocks.Redis;
using FluentAssertions;

namespace CarRental.BuildingBlocks.Tests;

public class RedisPrimitivesTests
{
    [Fact]
    public async Task InMemoryDistributedLockProvider_ShouldEnforceMutualExclusionAndAllowReacquireAfterDispose()
    {
        var provider = new InMemoryDistributedLockProvider();
        const string lockKey = "lock:availability:vehicle:test-1";

        await using (var first = await provider.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(10), TimeSpan.Zero))
        {
            first.Should().NotBeNull();
            first!.Key.Should().Be(lockKey);

            var second = await provider.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(50));
            second.Should().BeNull();

            // Double dispose of unacquired handle didn't happen; double dispose of first handle should be idempotent.
            await first.DisposeAsync();
        }

        await using var reacquired = await provider.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(10), TimeSpan.Zero);
        reacquired.Should().NotBeNull();
    }

    [Fact]
    public async Task InMemoryCacheService_ShouldRespectTtlRemoveAndVersionCounters()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        var cache = new InMemoryCacheService(clock);

        await cache.SetAsync("pricing:plan:v1", new SamplePayload("Camry", 800_000m), TimeSpan.FromMinutes(5));
        var hit = await cache.GetAsync<SamplePayload>("pricing:plan:v1");
        hit.Should().Be(new SamplePayload("Camry", 800_000m));

        // Advance past TTL -> cache miss
        clock.Advance(TimeSpan.FromMinutes(6));
        var expired = await cache.GetAsync<SamplePayload>("pricing:plan:v1");
        expired.Should().BeNull();

        var calls = 0;
        var loaded1 = await cache.GetOrSetAsync(
            "fleet:brands:all",
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("Toyota", 1m));
            },
            TimeSpan.FromMinutes(10));
        var loaded2 = await cache.GetOrSetAsync(
            "fleet:brands:all",
            _ =>
            {
                calls++;
                return Task.FromResult(new SamplePayload("Honda", 2m));
            },
            TimeSpan.FromMinutes(10));

        calls.Should().Be(1);
        loaded1.Should().Be(loaded2);

        await cache.RemoveAsync("fleet:brands:all");
        (await cache.GetAsync<SamplePayload>("fleet:brands:all")).Should().BeNull();

        (await cache.GetVersionAsync("search:version")).Should().Be(0);
        (await cache.IncrementVersionAsync("search:version")).Should().Be(1);
        (await cache.IncrementVersionAsync("search:version")).Should().Be(2);
        (await cache.GetVersionAsync("search:version")).Should().Be(2);
    }

    [Fact]
    public async Task InMemoryTokenRevocationStore_ShouldTrackJtiAndTokensValidAfterWithTtl()
    {
        var now = new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var store = new InMemoryTokenRevocationStore(clock);

        var jti = Guid.CreateVersion7().ToString();
        (await store.IsJtiRevokedAsync(jti)).Should().BeFalse();

        await store.RevokeJtiAsync(jti, TimeSpan.FromMinutes(15));
        (await store.IsJtiRevokedAsync(jti)).Should().BeTrue();

        var userId = Guid.NewGuid();
        await store.SetTokensValidAfterAsync(userId, now, TimeSpan.FromMinutes(15));
        (await store.GetTokensValidAfterAsync(userId)).Should().Be(now);

        clock.Advance(TimeSpan.FromMinutes(16));
        (await store.IsJtiRevokedAsync(jti)).Should().BeFalse();
        (await store.GetTokensValidAfterAsync(userId)).Should().BeNull();
    }

    private sealed record SamplePayload(string Name, decimal Amount);

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}

