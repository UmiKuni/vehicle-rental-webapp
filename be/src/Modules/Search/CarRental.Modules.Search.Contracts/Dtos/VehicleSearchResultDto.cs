using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Search.Contracts.Dtos;

public sealed record VehicleSearchResultDto(
    Guid VehicleId,
    string Brand,
    string Model,
    int Seats,
    string Transmission,
    GeoPoint Location,
    string Address,
    decimal PriceFrom,
    decimal AverageRating,
    int TotalReviews,
    double DistanceKm);
