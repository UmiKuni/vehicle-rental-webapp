using System.Collections.Concurrent;

namespace CarRental.Api.Infrastructure.RateLimiting;

internal sealed class InMemoryRateLimiter : IRateLimiter
{
    private sealed class BucketState
    {
        public int Count;
        public DateTimeOffset WindowExpiresAt;
        public readonly object Sync = new();
    }

    private readonly ConcurrentDictionary<string, BucketState> _buckets = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public InMemoryRateLimiter(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task<RateLimitDecision> CheckAsync(
        string policyName,
        string partitionKey,
        int permitLimit,
        TimeSpan window,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ct.ThrowIfCancellationRequested();

        if (permitLimit <= 0 || window <= TimeSpan.Zero)
            return Task.FromResult(new RateLimitDecision(true, Math.Max(0, permitLimit), Math.Max(0, permitLimit), 0));

        var key = $"rl:{policyName}:{partitionKey}";
        var bucket = _buckets.GetOrAdd(key, _ => new BucketState());
        var now = _clock.GetUtcNow();

        lock (bucket.Sync)
        {
            if (now >= bucket.WindowExpiresAt)
            {
                bucket.Count = 1;
                bucket.WindowExpiresAt = now.Add(window);
            }
            else
            {
                bucket.Count++;
            }

            var isAllowed = bucket.Count <= permitLimit;
            var remaining = Math.Max(0, permitLimit - Math.Min(bucket.Count, permitLimit));
            var retryAfter = Math.Max(1, (int)Math.Ceiling((bucket.WindowExpiresAt - now).TotalSeconds));

            return Task.FromResult(new RateLimitDecision(isAllowed, permitLimit, remaining, retryAfter));
        }
    }

    public void Clear() => _buckets.Clear();
}
