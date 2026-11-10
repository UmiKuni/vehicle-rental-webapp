using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Search.Contracts.Dtos;

namespace CarRental.Modules.Search.Contracts;

public interface ISearchApi
{
    Task<Result<PagedVehicleSearchResultDto>> SearchVehiclesAsync(
        VehicleSearchRequest request,
        CancellationToken ct = default);
}
