using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Reviews.Errors;

internal static class ReviewsErrors
{
    public static Error InvalidBooking => Error.Validation("review.invalid_booking", "Reviewable booking context is required.");
    public static Error NotFound => Error.NotFound("review.not_found", "Review was not found.");
    public static Error InvalidPaging => Error.Validation("review.invalid_paging", "Page must be at least 1 and page size must be between 1 and 100.");

    public static Error Unauthorized => Error.Unauthorized("review.unauthorized", "Only the renter who completed the trip can review this vehicle.");

    public static Error AlreadyReviewed => Error.Conflict("review.already_reviewed", "This booking has already been reviewed.");

    public static Error InvalidRating => Error.Validation("review.invalid_rating", "Rating must be between 1 and 5 stars.");

    public static Error EmptyContent => Error.Validation("review.empty_content", "Review content cannot be empty.");

    public static Error InvalidAdmin => Error.Validation("review.invalid_admin", "Admin ID is required for moderation.");

    public static Error MissingReason => Error.Validation("review.missing_reason", "A moderation reason is required to hide a review.");

}
