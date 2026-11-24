using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CarRental.Modules.Search.Contracts.Dtos;

namespace CarRental.Modules.Search.Services;

internal static class SearchCacheKeys
{
    public const string VersionKey = "search:version";
    public static readonly TimeSpan SearchTtl = TimeSpan.FromSeconds(45);

    public static string BuildQueryKey(long version, VehicleSearchRequest request)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{request.Brand?.Trim().ToLowerInvariant()}|{request.Model?.Trim().ToLowerInvariant()}|{request.Seats}|{request.Transmission?.Trim()}|{request.MinPrice}|{request.MaxPrice}|{request.Latitude}|{request.Longitude}|{request.RadiusKm}|{request.Page}|{request.PageSize}");

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var queryHash = Convert.ToHexStringLower(hashBytes);
        return $"search:v{version}:{queryHash}";
    }
}
