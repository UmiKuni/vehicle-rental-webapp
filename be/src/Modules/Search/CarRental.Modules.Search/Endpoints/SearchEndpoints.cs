using CarRental.Modules.Search.Contracts;
using CarRental.Modules.Search.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Search.Endpoints;

public static class SearchEndpoints
{
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search/vehicles", async (
            [AsParameters] VehicleSearchRequest request,
            ISearchApi api,
            CancellationToken ct) =>
        {
            var result = await api.SearchVehiclesAsync(request, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error.Message);
        })
        .WithTags("Search")
        .WithName("SearchVehicles")
        .WithSummary("Search published vehicles by specifications, price, and radius")
        .Produces<PagedVehicleSearchResultDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        return app;
    }
}
