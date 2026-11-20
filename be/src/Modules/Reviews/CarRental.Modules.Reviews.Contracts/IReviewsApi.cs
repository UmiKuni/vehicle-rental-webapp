using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reviews.Contracts.Dtos;

namespace CarRental.Modules.Reviews.Contracts;

public interface IReviewsApi
{
    Task<Result<ReviewDto>> CreateAsync(
        Guid bookingId,
        Guid customerId,
        CreateReviewDto request,
        CancellationToken ct = default);

    Task<Result<PagedResult<ReviewDto>>> GetVehicleReviewsAsync(
        Guid vehicleId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    Task<Result<RatingSummaryDto>> GetRatingSummaryAsync(
        Guid vehicleId,
        CancellationToken ct = default);

    Task<Result> HideAsync(
        Guid reviewId,
        Guid adminId,
        string reason,
        CancellationToken ct = default);

    Task<Result> RestoreAsync(
        Guid reviewId,
        Guid adminId,
        CancellationToken ct = default);
}
