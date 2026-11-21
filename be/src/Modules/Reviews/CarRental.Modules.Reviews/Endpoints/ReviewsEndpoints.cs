using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reviews.Contracts;
using CarRental.Modules.Reviews.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Reviews.Endpoints;

public static class ReviewsEndpoints
{
    public static IEndpointRouteBuilder MapReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reviews").WithTags("Reviews");

        group.MapPost("/bookings/{bookingId:guid}", async (
            Guid bookingId,
            CreateReviewDto request,
            ClaimsPrincipal principal,
            IReviewsApi api,
            CancellationToken ct) =>
            (await api.CreateAsync(bookingId, GetUserId(principal), request, ct)).ToCreatedHttpResult(
                review => $"/api/reviews/{review.ReviewId}"))
            .RequireAuthorization("CustomerOnly")
            .Produces<ReviewDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/vehicles/{vehicleId:guid}", async (
            Guid vehicleId,
            IReviewsApi api,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
            (await api.GetVehicleReviewsAsync(vehicleId, page, pageSize, ct)).ToHttpResult())
            .Produces<PagedResult<ReviewDto>>(StatusCodes.Status200OK);

        group.MapGet("/vehicles/{vehicleId:guid}/summary", async (
            Guid vehicleId,
            IReviewsApi api,
            CancellationToken ct) =>
            (await api.GetRatingSummaryAsync(vehicleId, ct)).ToHttpResult())
            .Produces<RatingSummaryDto>(StatusCodes.Status200OK);

        group.MapPut("/{reviewId:guid}/hide", async (
            Guid reviewId,
            string reason,
            ClaimsPrincipal principal,
            IReviewsApi api,
            CancellationToken ct) =>
            (await api.HideAsync(reviewId, GetUserId(principal), reason, ct)).ToHttpResult())
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{reviewId:guid}/restore", async (
            Guid reviewId,
            ClaimsPrincipal principal,
            IReviewsApi api,
            CancellationToken ct) =>
            (await api.RestoreAsync(reviewId, GetUserId(principal), ct)).ToHttpResult())
            .RequireAuthorization("AdminOnly")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
}
