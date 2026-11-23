using System.Collections.Concurrent;
using System.Text.Json;

namespace CarRental.BuildingBlocks.Redis;

/// <summary>
/// Process-local <see cref="ICacheService"/> fallback for Testing and unconfigured local environments.
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private sealed record CacheItem(string JsonPayload, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheItem> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public InMemoryCacheService(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (!_entries.TryGetValue(key, out var item))
            return Task.FromResult<T?>(default);

        if (item.ExpiresAt <= _clock.GetUtcNow())
        {
            _entries.TryRemove(new KeyValuePair<string, CacheItem>(key, item));
            return Task.FromResult<T?>(default);
        }

        return Task.FromResult(JsonSerializer.Deserialize<T>(item.JsonPayload, SerializerOptions));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return Task.CompletedTask;

        var payload = JsonSerializer.Serialize(value, SerializerOptions);
        var expiresAt = _clock.GetUtcNow().Add(ttl);
        _entries[key] = new CacheItem(payload, expiresAt);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var created = await factory(ct);
        if (created is not null)
            await SetAsync(key, created, ttl, ct);

        return created;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<long> GetVersionAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_versions.GetValueOrDefault(key, 0L));
    }

    public Task<long> IncrementVersionAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        var updated = _versions.AddOrUpdate(key, 1L, static (_, current) => current + 1L);
        return Task.FromResult(updated);
    }

    public void Clear()
    {
        _entries.Clear();
        _versions.Clear();
    }
}

