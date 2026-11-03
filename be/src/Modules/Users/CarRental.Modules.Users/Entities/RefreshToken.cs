using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Users.Entities;

public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? DeviceInfo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActive => RevokedAt == null && DateTimeOffset.UtcNow < ExpiresAt;

    private RefreshToken() { }

    public RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? deviceInfo,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        DeviceInfo = deviceInfo;
        CreatedAt = createdAt;
        RevokedAt = null;
        ReplacedByTokenId = null;
    }

    public void Revoke(Guid? replacedByTokenId = null, DateTimeOffset? revokedAt = null)
    {
        RevokedAt = revokedAt ?? DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
