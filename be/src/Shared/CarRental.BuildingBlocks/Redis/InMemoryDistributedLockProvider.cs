using System.Collections.Concurrent;

namespace CarRental.BuildingBlocks.Redis;

/// <summary>
/// Process-local <see cref="IDistributedLockProvider"/> backed by keyed <see cref="SemaphoreSlim"/> instances.
/// </summary>
public sealed class InMemoryDistributedLockProvider : IDistributedLockProvider
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        TimeSpan waitTimeout,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var semaphore = _semaphores.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        var acquired = waitTimeout <= TimeSpan.Zero
            ? await semaphore.WaitAsync(0, ct)
            : await semaphore.WaitAsync(waitTimeout, ct);

        return acquired ? new InMemoryLockHandle(key, semaphore) : null;
    }

    private sealed class InMemoryLockHandle(string key, SemaphoreSlim semaphore) : IDistributedLockHandle
    {
        private int _disposed;

        public string Key { get; } = key;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}

