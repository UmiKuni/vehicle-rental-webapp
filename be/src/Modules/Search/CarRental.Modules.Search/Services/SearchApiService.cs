using CarRental.Modules.Search.Errors;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Search.Contracts;
using CarRental.Modules.Search.Contracts.Dtos;
using CarRental.Modules.Search.Data;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Search.Services;

public sealed class SearchApiService : ISearchApi
{
    private readonly SearchDbContext _dbContext;

    public SearchApiService(SearchDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedVehicleSearchResultDto>> SearchVehiclesAsync(
        VehicleSearchRequest request,
        CancellationToken ct = default)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            return Result.Failure<PagedVehicleSearchResultDto>(
                SearchErrors.InvalidPaging);

        if (request.MinPrice.HasValue && request.MinPrice < 0 ||
            request.MaxPrice.HasValue && request.MaxPrice < 0 ||
            request.MinPrice.HasValue && request.MaxPrice.HasValue && request.MinPrice > request.MaxPrice)
            return Result.Failure<PagedVehicleSearchResultDto>(
                SearchErrors.InvalidPriceRange);

        GeoPoint? center = null;
        if (request.Latitude.HasValue || request.Longitude.HasValue || request.RadiusKm.HasValue)
        {
            if (!request.Latitude.HasValue || !request.Longitude.HasValue ||
                !request.RadiusKm.HasValue || request.RadiusKm <= 0)
                return Result.Failure<PagedVehicleSearchResultDto>(
                    SearchErrors.InvalidLocation);

            var centerResult = GeoPoint.Create(request.Latitude.Value, request.Longitude.Value);
            if (centerResult.IsFailure)
                return Result.Failure<PagedVehicleSearchResultDto>(centerResult.Error);

            center = centerResult.Value;
        }

        var query = _dbContext.VehicleSearchDocuments
            .AsNoTracking()
            .Where(d => d.IsPublished && d.PriceFrom.HasValue);

        if (!string.IsNullOrWhiteSpace(request.Brand))
            query = query.Where(d => d.Brand.ToLower().Contains(request.Brand.Trim().ToLower()));
        if (!string.IsNullOrWhiteSpace(request.Model))
            query = query.Where(d => d.Model.ToLower().Contains(request.Model.Trim().ToLower()));
        if (request.Seats.HasValue)
            query = query.Where(d => d.Seats == request.Seats.Value);
        if (!string.IsNullOrWhiteSpace(request.Transmission))
            query = query.Where(d => d.Transmission == request.Transmission.Trim());
        if (request.MinPrice.HasValue)
            query = query.Where(d => d.PriceFrom >= request.MinPrice.Value);
        if (request.MaxPrice.HasValue)
            query = query.Where(d => d.PriceFrom <= request.MaxPrice.Value);

        if (center is not null)
        {
            var radius = request.RadiusKm!.Value;
            var latitudeDelta = radius / 111.045;
            var longitudeDelta = radius / (111.045 * Math.Max(Math.Cos(center.Latitude * Math.PI / 180), 0.01));
            query = query.Where(d =>
                d.Location.Latitude >= center.Latitude - latitudeDelta &&
                d.Location.Latitude <= center.Latitude + latitudeDelta &&
                d.Location.Longitude >= center.Longitude - longitudeDelta &&
                d.Location.Longitude <= center.Longitude + longitudeDelta);
        }

        var documents = await query
            .OrderByDescending(d => d.AverageRating ?? 0)
            .ThenBy(d => d.PriceFrom)
            .ToListAsync(ct);

        var results = documents
            .Select(d => new
            {
                Document = d,
                DistanceKm = center is null ? 0 : d.Location.DistanceToInKilometers(center)
            })
            .Where(item => center is null || item.DistanceKm <= request.RadiusKm)
            .ToList();

        var totalCount = results.Count;
        var items = results
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(item => ToDto(item.Document, item.DistanceKm))
            .ToList();

        return Result.Success(new PagedVehicleSearchResultDto(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (int)Math.Ceiling(totalCount / (double)request.PageSize)));
    }

    private static VehicleSearchResultDto ToDto(
        Entities.VehicleSearchDocument document,
        double distanceKm) =>
        new(
            document.VehicleId,
            document.Brand,
            document.Model,
            document.Seats,
            document.Transmission,
            document.Location,
            document.Address,
            document.PriceFrom!.Value,
            document.AverageRating ?? 0,
            document.TotalReviews,
            distanceKm);
}
