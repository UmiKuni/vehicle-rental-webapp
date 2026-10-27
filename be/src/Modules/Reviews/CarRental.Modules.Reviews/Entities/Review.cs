using CarRental.Modules.Reviews.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reviews.Contracts.Enums;

namespace CarRental.Modules.Reviews.Entities;

public class Review : AggregateRoot<ReviewId>
{
    public Guid BookingId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid CustomerId { get; private set; }
    public int Rating { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public ReviewStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? ModerationReason { get; private set; }
    public Guid? ModeratedBy { get; private set; }

    private Review() { }

    private Review(
        ReviewId id,
        Guid bookingId,
        Guid vehicleId,
        Guid customerId,
        int rating,
        string content,
        DateTimeOffset now)
    {
        Id = id;
        BookingId = bookingId;
        VehicleId = vehicleId;
        CustomerId = customerId;
        Rating = rating;
        Content = content.Trim();
        Status = ReviewStatus.Published;
        CreatedAt = now;
    }

    public static Result<Review> Create(
        ReviewableBooking booking,
        Guid customerId,
        int rating,
        string content,
        DateTimeOffset now)
    {
        if (booking == null)
            return Result.Failure<Review>(ReviewsErrors.InvalidBooking);

        if (customerId == Guid.Empty || customerId != booking.CustomerId)
            return Result.Failure<Review>(ReviewsErrors.Unauthorized);

        if (booking.HasReviewed)
            return Result.Failure<Review>(ReviewsErrors.AlreadyReviewed);

        if (rating < 1 || rating > 5)
            return Result.Failure<Review>(ReviewsErrors.InvalidRating);

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure<Review>(ReviewsErrors.EmptyContent);

        booking.MarkReviewed();
        return Result.Success(new Review(
            ReviewId.New(),
            booking.BookingId,
            booking.VehicleId,
            customerId,
            rating,
            content,
            now));
    }

    public Result Hide(Guid adminId, string reason)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(ReviewsErrors.InvalidAdmin4CF5BB);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure(ReviewsErrors.MissingReason);

        Status = ReviewStatus.Hidden;
        ModeratedBy = adminId;
        ModerationReason = reason.Trim();

        return Result.Success();
    }

    public Result Restore(Guid adminId)
    {
        if (adminId == Guid.Empty)
            return Result.Failure(ReviewsErrors.InvalidAdmin21FF89);

        Status = ReviewStatus.Published;
        ModeratedBy = adminId;
        ModerationReason = null;

        return Result.Success();
    }
}
