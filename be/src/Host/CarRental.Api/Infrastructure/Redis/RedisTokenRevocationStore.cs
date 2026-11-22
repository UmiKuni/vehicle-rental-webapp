using System.Globalization;
using CarRental.BuildingBlocks.Auth;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CarRental.Api.Infrastructure.Redis;

internal sealed class RedisTokenRevocationStore : ITokenRevocationStore
{
    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly ILogger<RedisTokenRevocationStore> _logger;

    public RedisTokenRevocationStore(
        IConnectionMultiplexer multiplexer,
        IOptions<RedisOptions> options,
        ILogger<RedisTokenRevocationStore> logger)
    {
        _database = multiplexer.GetDatabase();
        _prefix = RedisCacheService.NormalizePrefix(options.Value.KeyPrefix);
        _logger = logger;
    }

    public async Task RevokeJtiAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return;

        try
        {
            await _database.StringSetAsync(BuildJtiKey(jti), "1", ttl).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis JTI revocation failed for {Jti}.", jti);
        }
    }

    public async Task<bool> IsJtiRevokedAsync(string jti, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ct.ThrowIfCancellationRequested();

        try
        {
            return await _database.KeyExistsAsync(BuildJtiKey(jti)).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis JTI revocation check failed for {Jti}; treating as not revoked.", jti);
            return false;
        }
    }

    public async Task SetTokensValidAfterAsync(
        Guid userId,
        DateTimeOffset validAfter,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return;

        try
        {
            var epochSeconds = validAfter.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            await _database.StringSetAsync(BuildValidAfterKey(userId), epochSeconds, ttl).WaitAsync(ct);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis tokens_valid_after update failed for user {UserId}.", userId);
        }
    }

    public async Task<DateTimeOffset?> GetTokensValidAfterAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var value = await _database.StringGetAsync(BuildValidAfterKey(userId)).WaitAsync(ct);
            if (!value.HasValue || !long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var epochSeconds))
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
        }
        catch (Exception ex) when (ex is RedisException or RedisTimeoutException)
        {
            _logger.LogWarning(ex, "Redis tokens_valid_after check failed for user {UserId}.", userId);
            return null;
        }
    }

    private string BuildJtiKey(string jti) => $"{_prefix}revoked:jti:{jti}";

    private string BuildValidAfterKey(Guid userId) => $"{_prefix}user:tokens_valid_after:{userId:N}";
}

