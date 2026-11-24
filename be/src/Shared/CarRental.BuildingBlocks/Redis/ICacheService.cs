namespace CarRental.BuildingBlocks.Redis;

/// <summary>
/// Distributed or in-memory cache abstraction for cross-module read caching and versioned invalidation.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);

    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task<long> GetVersionAsync(string key, CancellationToken ct = default);

    Task<long> IncrementVersionAsync(string key, CancellationToken ct = default);
}

