using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reviews.Contracts;
using CarRental.Modules.Reviews.Contracts.Dtos;
using CarRental.Modules.Reviews.Contracts.Enums;
using CarRental.Modules.Reviews.Contracts.Events;
using CarRental.Modules.Reviews.Data;
using CarRental.Modules.Reviews.Entities;
using CarRental.Modules.Reviews.Errors;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Reviews.Services;

internal sealed class ReviewsApiService : IReviewsApi
{
    private readonly ReviewsDbContext _dbContext;
    private readonly IEventOutbox _eventOutbox;
    private readonly TimeProvider _clock;

    public ReviewsApiService(
        ReviewsDbContext dbContext,
        IEventOutbox eventOutbox,
        TimeProvider clock)
    {
        _dbContext = dbContext;
        _eventOutbox = eventOutbox;
        _clock = clock;
    }

    public async Task<Result<ReviewDto>> CreateAsync(
        Guid bookingId,
        Guid customerId,
        CreateReviewDto request,
        CancellationToken ct = default)
    {
        var booking = await _dbContext.ReviewableBookings
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, ct);
        if (booking is null)
            return Result.Failure<ReviewDto>(ReviewsErrors.InvalidBooking);

        var reviewResult = Review.Create(
            booking,
            customerId,
            request.Rating,
            request.Content,
            _clock.GetUtcNow());
        if (reviewResult.IsFailure)
            return Result.Failure<ReviewDto>(reviewResult.Error);

        var summary = await _dbContext.RatingSummaries
            .FirstOrDefaultAsync(item => item.Id == booking.VehicleId, ct);
        if (summary is null)
        {
            summary = RatingSummary.Create(booking.VehicleId);
            _dbContext.RatingSummaries.Add(summary);
        }

        summary.AddRating(reviewResult.Value.Rating);
        _dbContext.Reviews.Add(reviewResult.Value);
        await _dbContext.SaveChangesAsync(ct);
        await PublishSummaryAsync(summary, ct);

        return Result.Success(ToDto(reviewResult.Value));
    }

    public async Task<Result<PagedResult<ReviewDto>>> GetVehicleReviewsAsync(
        Guid vehicleId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return Result.Failure<PagedResult<ReviewDto>>(ReviewsErrors.InvalidPaging);

        var query = _dbContext.Reviews
            .AsNoTracking()
            .Where(item => item.VehicleId == vehicleId && item.Status == ReviewStatus.Published)
            .OrderByDescending(item => item.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ReviewDto>(
            items.Select(ToDto).ToList(),
            totalCount,
            page,
            pageSize));
    }

    public async Task<Result<RatingSummaryDto>> GetRatingSummaryAsync(
        Guid vehicleId,
        CancellationToken ct = default)
    {
        var summary = await _dbContext.RatingSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == vehicleId, ct);

        return Result.Success(summary is null
            ? new RatingSummaryDto(vehicleId, 0m, 0)
            : new RatingSummaryDto(vehicleId, summary.AverageRating, summary.TotalReviews));
    }

    public async Task<Result> HideAsync(
        Guid reviewId,
        Guid adminId,
        string reason,
        CancellationToken ct = default)
    {
        var review = await _dbContext.Reviews
            .FirstOrDefaultAsync(item => item.Id == ReviewId.From(reviewId), ct);
        if (review is null)
            return Result.Failure(ReviewsErrors.NotFound);

        var result = review.Hide(adminId, reason);
        if (result.IsFailure)
            return result;

        var summary = await _dbContext.RatingSummaries
            .FirstOrDefaultAsync(item => item.Id == review.VehicleId, ct);
        summary?.RemoveRating(review.Rating);

        await _dbContext.SaveChangesAsync(ct);
        if (summary is not null)
            await PublishSummaryAsync(summary, ct);

        return Result.Success();
    }

    public async Task<Result> RestoreAsync(
        Guid reviewId,
        Guid adminId,
        CancellationToken ct = default)
    {
        var review = await _dbContext.Reviews
            .FirstOrDefaultAsync(item => item.Id == ReviewId.From(reviewId), ct);
        if (review is null)
            return Result.Failure(ReviewsErrors.NotFound);

        var result = review.Restore(adminId);
        if (result.IsFailure)
            return result;

        var summary = await _dbContext.RatingSummaries
            .FirstOrDefaultAsync(item => item.Id == review.VehicleId, ct);
        if (summary is null)
        {
            summary = RatingSummary.Create(review.VehicleId);
            _dbContext.RatingSummaries.Add(summary);
        }

        summary.AddRating(review.Rating);
        await _dbContext.SaveChangesAsync(ct);
        await PublishSummaryAsync(summary, ct);
        return Result.Success();
    }

    private async Task PublishSummaryAsync(RatingSummary summary, CancellationToken ct) =>
        await _eventOutbox.PublishAsync(
            new RatingSummaryUpdated(
                Guid.CreateVersion7(),
                _clock.GetUtcNow(),
                summary.VehicleId,
                summary.AverageRating,
                summary.TotalReviews),
            ct);

    private static ReviewDto ToDto(Review review) =>
        new(
            review.Id.Value,
            review.BookingId,
            review.VehicleId,
            review.CustomerId,
            review.Rating,
            review.Content,
            review.Status,
            review.CreatedAt,
            review.ModerationReason);
}
