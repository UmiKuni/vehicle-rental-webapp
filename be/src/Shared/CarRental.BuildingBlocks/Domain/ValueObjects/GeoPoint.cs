using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Value object representing a geographic point with WGS84 coordinates (Latitude and Longitude).
/// Maps to PostgreSQL PostGIS geography(Point, 4326).
/// </summary>
public sealed record GeoPoint : ValueObject
{
    private const double EarthRadiusKm = 6371.0;

    public double Latitude { get; init; }
    public double Longitude { get; init; }

    /// <summary>
    /// Parameterless constructor required by EF Core.
    /// </summary>
    private GeoPoint()
    {
    }

    public GeoPoint(double latitude, double longitude)
    {
        Guard.Against(latitude < -90.0 || latitude > 90.0, "geopoint.invalid_latitude", "Latitude must be between -90 and 90 degrees.");
        Guard.Against(longitude < -180.0 || longitude > 180.0, "geopoint.invalid_longitude", "Longitude must be between -180 and 180 degrees.");

        Latitude = latitude;
        Longitude = longitude;
    }

    public static Result<GeoPoint> Create(double latitude, double longitude)
    {
        if (latitude < -90.0 || latitude > 90.0)
        {
            return Result<GeoPoint>.Failure(
                new Error("geopoint.invalid_latitude", "Latitude must be between -90 and 90 degrees."));
        }

        if (longitude < -180.0 || longitude > 180.0)
        {
            return Result<GeoPoint>.Failure(
                new Error("geopoint.invalid_longitude", "Longitude must be between -180 and 180 degrees."));
        }

        return Result<GeoPoint>.Success(new GeoPoint(latitude, longitude));
    }

    /// <summary>
    /// Calculates the great-circle distance between two points using the Haversine formula in kilometers.
    /// </summary>
    public double DistanceToInKilometers(GeoPoint other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);

        var lat1 = ToRadians(Latitude);
        var lat2 = ToRadians(other.Latitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2) * Math.Cos(lat1) * Math.Cos(lat2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Calculates the distance between two points in meters.
    /// </summary>
    public double DistanceToInMeters(GeoPoint other) => DistanceToInKilometers(other) * 1000.0;

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    public override string ToString() => $"({Latitude:F6}, {Longitude:F6})";
}
