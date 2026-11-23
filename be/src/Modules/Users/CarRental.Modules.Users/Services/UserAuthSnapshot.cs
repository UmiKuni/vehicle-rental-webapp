using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Services;

internal sealed record UserAuthSnapshot(
    UserStatus Status,
    UserRole Role,
    bool IsIdVerified,
    bool IsDriverLicenseVerified)
{
    public bool IsVerified => IsIdVerified && IsDriverLicenseVerified;
}

internal static class UserAuthCacheKeys
{
    public static readonly TimeSpan AuthSnapshotTtl = TimeSpan.FromMinutes(3);

    public static string AuthSnapshot(Guid userId) => $"auth:user:{userId:D}";
}
