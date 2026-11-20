using CarRental.Modules.Reviews.Contracts.Enums;

namespace CarRental.Modules.Reviews.Contracts.Dtos;

public sealed record CreateReviewDto(int Rating, string Content);

public sealed record ReviewDto(
    Guid ReviewId,
    Guid BookingId,
    Guid VehicleId,
    Guid CustomerId,
    int Rating,
    string Content,
    ReviewStatus Status,
    DateTimeOffset CreatedAt,
    string? ModerationReason);

public sealed record RatingSummaryDto(
    Guid VehicleId,
    decimal AverageRating,
    int TotalReviews);
