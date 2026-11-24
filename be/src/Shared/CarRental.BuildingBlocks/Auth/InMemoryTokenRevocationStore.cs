using System.Collections.Concurrent;

namespace CarRental.BuildingBlocks.Auth;

/// <summary>
/// Process-local <see cref="ITokenRevocationStore"/> fallback for Testing and unconfigured local environments.
/// </summary>
public sealed class InMemoryTokenRevocationStore : ITokenRevocationStore
{
    private sealed record ValidAfterEntry(DateTimeOffset ValidAfter, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _revokedJtis = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, ValidAfterEntry> _tokensValidAfter = new();
    private readonly TimeProvider _clock;

    public InMemoryTokenRevocationStore(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public Task RevokeJtiAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return Task.CompletedTask;

        _revokedJtis[jti] = _clock.GetUtcNow().Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsJtiRevokedAsync(string jti, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);
        ct.ThrowIfCancellationRequested();

        if (!_revokedJtis.TryGetValue(jti, out var expiresAt))
            return Task.FromResult(false);

        if (expiresAt <= _clock.GetUtcNow())
        {
            _revokedJtis.TryRemove(new KeyValuePair<string, DateTimeOffset>(jti, expiresAt));
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    public Task SetTokensValidAfterAsync(
        Guid userId,
        DateTimeOffset validAfter,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        ct.ThrowIfCancellationRequested();

        if (ttl <= TimeSpan.Zero)
            return Task.CompletedTask;

        _tokensValidAfter[userId] = new ValidAfterEntry(validAfter, _clock.GetUtcNow().Add(ttl));
        return Task.CompletedTask;
    }

    public Task<DateTimeOffset?> GetTokensValidAfterAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_tokensValidAfter.TryGetValue(userId, out var entry))
            return Task.FromResult<DateTimeOffset?>(null);

        if (entry.ExpiresAt <= _clock.GetUtcNow())
        {
            _tokensValidAfter.TryRemove(new KeyValuePair<Guid, ValidAfterEntry>(userId, entry));
            return Task.FromResult<DateTimeOffset?>(null);
        }

        return Task.FromResult<DateTimeOffset?>(entry.ValidAfter);
    }

    public void Clear()
    {
        _revokedJtis.Clear();
        _tokensValidAfter.Clear();
    }
}

