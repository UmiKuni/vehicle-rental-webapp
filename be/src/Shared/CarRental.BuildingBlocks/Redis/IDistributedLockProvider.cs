namespace CarRental.BuildingBlocks.Redis;

/// <summary>
/// Represents an acquired lock handle released asynchronously upon disposal.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable
{
    string Key { get; }
}

/// <summary>
/// Provides mutual exclusion across process or replica boundaries.
/// </summary>
public interface IDistributedLockProvider
{
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        TimeSpan waitTimeout,
        CancellationToken ct = default);
}

