using CarRental.BuildingBlocks.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.Redis;

internal sealed class RedisDistributedLockProvider : IDistributedLockProvider
{
    private const string ReleaseScript =
        "if redis.call(\"get\", KEYS[1]) == ARGV[1] then return redis.call(\"del\", KEYS[1]) else return 0 end";

    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly ILogger<RedisDistributedLockProvider> _logger;

    public RedisDistributedLockProvider(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisDistributedLockProvider> logger)
    {
        _database = multiplexer.GetDatabase();
        _prefix = RedisCacheService.NormalizePrefix(options.Value.KeyPrefix);
        _logger = logger;
    }

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        TimeSpan waitTimeout,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "Lock TTL must be positive.");

        var fullKey = $"{_prefix}{key}";
        var token = Guid.NewGuid().ToString("N");
        var deadline = waitTimeout > TimeSpan.Zero
            ? DateTimeOffset.UtcNow.Add(waitTimeout)
            : DateTimeOffset.MinValue;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var acquired = await _database.StringSetAsync(fullKey, token, ttl, When.NotExists).WaitAsync(ct);
                if (acquired)
                    return new RedisLockHandle(key, fullKey, token, _database, _logger);
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                // Fail open so authoritative PostgreSQL constraints/OCC continue serving requests.
                _logger.LogWarning(ex, "Redis lock acquisition failed for key {LockKey}; failing open to database concurrency controls.", key);
                return new NoOpLockHandle(key);
            }

            if (waitTimeout <= TimeSpan.Zero || DateTimeOffset.UtcNow >= deadline)
                return null;

            var delayMs = Random.Shared.Next(40, 91);
            await Task.Delay(delayMs, ct);
        }
    }

    private sealed class RedisLockHandle(
        string key,
        string fullKey,
        string token,
        IDatabase database,
        ILogger logger) : IDistributedLockHandle
    {
        private int _disposed;

        public string Key { get; } = key;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                await database.ScriptEvaluateAsync(
                    ReleaseScript,
                    new RedisKey[] { fullKey },
                    new RedisValue[] { token });
            }
            catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
            {
                logger.LogWarning(ex, "Redis lock release failed for key {LockKey}; lock will expire via TTL.", Key);
            }
        }
    }

    private sealed class NoOpLockHandle(string key) : IDistributedLockHandle
    {
        public string Key { get; } = key;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

