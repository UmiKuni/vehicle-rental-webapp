using System.Text.Json;
using CarRental.BuildingBlocks.Redis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.Redis;

internal sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _database = multiplexer.GetDatabase();
        _prefix = NormalizePrefix(options.Value.KeyPrefix);
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await _database.StringGetAsync(BuildKey(key)).WaitAsync(ct);
            if (!value.HasValue)
                return default;

            return JsonSerializer.Deserialize<T>(value.ToString(), SerializerOptions);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis cache read failed for key {CacheKey}; treating as cache miss.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return;

        try
        {
            var payload = JsonSerializer.Serialize(value, SerializerOptions);
            await _database.StringSetAsync(BuildKey(key), payload, ttl).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis cache write failed for key {CacheKey}; skipping cache population.", key);
        }
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

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            await _database.KeyDeleteAsync(BuildKey(key)).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis cache delete failed for key {CacheKey}.", key);
        }
    }

    public async Task<long> GetVersionAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await _database.StringGetAsync(BuildKey(key)).WaitAsync(ct);
            return value.HasValue && long.TryParse(value.ToString(), out var version) ? version : 0L;
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis version read failed for key {VersionKey}; defaulting to 0.", key);
            return 0L;
        }
    }

    public async Task<long> IncrementVersionAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ct.ThrowIfCancellationRequested();

        try
        {
            return await _database.StringIncrementAsync(BuildKey(key)).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis version increment failed for key {VersionKey}; returning 0.", key);
            return 0L;
        }
    }

    private string BuildKey(string key) => $"{_prefix}{key}";

    internal static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        var trimmed = prefix.Trim();
        return trimmed.EndsWith(':') ? trimmed : $"{trimmed}:";
    }
}

