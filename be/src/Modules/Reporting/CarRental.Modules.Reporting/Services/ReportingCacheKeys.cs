namespace CarRental.Modules.Reporting.Services;

internal static class ReportingCacheKeys
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public static string AdminDashboard(int year, int month) =>
        $"reporting:admin:dashboard:{year:D4}-{month:D2}";

    public static string OwnerRevenue(Guid ownerId, int year, int month) =>
        $"reporting:owner:revenue:{ownerId:D}:{year:D4}-{month:D2}";
}
