namespace CarRental.BuildingBlocks.Auth;

/// <summary>
/// Stores revoked JWT identifiers (<c>jti</c>) and per-user token validity cutoffs.
/// </summary>
public interface ITokenRevocationStore
{
    Task RevokeJtiAsync(string jti, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> IsJtiRevokedAsync(string jti, CancellationToken ct = default);

    Task SetTokensValidAfterAsync(
        Guid userId,
        DateTimeOffset validAfter,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task<DateTimeOffset?> GetTokensValidAfterAsync(Guid userId, CancellationToken ct = default);
}

