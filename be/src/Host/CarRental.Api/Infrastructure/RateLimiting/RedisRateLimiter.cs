using CarRental.Api.Infrastructure.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.RateLimiting;

internal sealed class RedisRateLimiter : IRateLimiter
{
    private const string FixedWindowScript = """
        local current = redis.call("incr", KEYS[1])
        if current == 1 then
            redis.call("pexpire", KEYS[1], ARGV[1])
        end
        local ttl = redis.call("pttl", KEYS[1])
        return {current, ttl}
        """;

    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly ILogger<RedisRateLimiter> _logger;

    public RedisRateLimiter(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisRateLimiter> logger)
    {
        _database = multiplexer.GetDatabase();
        _prefix = RedisCacheService.NormalizePrefix(options.Value.KeyPrefix);
        _logger = logger;
    }

    public async Task<RateLimitDecision> CheckAsync(
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
            return new RateLimitDecision(true, Math.Max(0, permitLimit), Math.Max(0, permitLimit), 0);

        var key = $"{_prefix}rl:{policyName}:{partitionKey}";
        var windowMs = (long)Math.Max(1, window.TotalMilliseconds);

        try
        {
            var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
                FixedWindowScript,
                new RedisKey[] { key },
                new RedisValue[] { windowMs }).WaitAsync(ct);

            var current = result is { Length: > 0 } ? (long)result[0] : 1L;
            var ttlMs = result is { Length: > 1 } ? (long)result[1] : windowMs;

            var isAllowed = current <= permitLimit;
            var remaining = Math.Max(0, permitLimit - (int)Math.Min(current, permitLimit));
            var retryAfterSeconds = ttlMs > 0
                ? (int)Math.Ceiling(ttlMs / 1000.0)
                : (int)Math.Ceiling(window.TotalSeconds);

            return new RateLimitDecision(isAllowed, permitLimit, remaining, retryAfterSeconds);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis rate limit check failed for policy {Policy}; failing open.", policyName);
            return new RateLimitDecision(true, permitLimit, permitLimit, 0);
        }
    }
}
