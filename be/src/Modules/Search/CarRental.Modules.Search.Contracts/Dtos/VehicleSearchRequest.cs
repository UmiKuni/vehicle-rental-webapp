namespace CarRental.Modules.Search.Contracts.Dtos;

public sealed record VehicleSearchRequest(
    string? Brand = null,
    string? Model = null,
    int? Seats = null,
    string? Transmission = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null,
    int Page = 1,
    int PageSize = 20);

public sealed record PagedVehicleSearchResultDto(
    IReadOnlyList<VehicleSearchResultDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
