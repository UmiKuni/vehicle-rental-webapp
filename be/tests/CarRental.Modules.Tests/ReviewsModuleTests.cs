using CarRental.Modules.Reviews.Contracts.Enums;
using CarRental.Modules.Reviews.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class ReviewsModuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static ReviewableBooking CreateTestReviewableBooking()
    {
        return new ReviewableBooking(BookingId, VehicleId, CustomerId, Now);
    }

    [Fact]
    public void Create_WithValidParameters_ShouldSucceedAndMarkBookingReviewed()
    {
        var booking = CreateTestReviewableBooking();

        var result = Review.Create(booking, CustomerId, 5, "Amazing car and smooth ride!", Now);

        result.IsSuccess.Should().BeTrue();
        var review = result.Value;
        review.BookingId.Should().Be(BookingId);
        review.VehicleId.Should().Be(VehicleId);
        review.CustomerId.Should().Be(CustomerId);
        review.Rating.Should().Be(5);
        review.Content.Should().Be("Amazing car and smooth ride!");
        review.Status.Should().Be(ReviewStatus.Published);
        booking.HasReviewed.Should().BeTrue();
    }

    [Fact]
    public void Create_WhenBookingIsNull_ShouldFail()
    {
        var result = Review.Create(null!, CustomerId, 5, "Good", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.invalid_booking");
    }

    [Fact]
    public void Create_WhenAlreadyReviewed_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();
        Review.Create(booking, CustomerId, 5, "First review", Now);

        var secondResult = Review.Create(booking, CustomerId, 4, "Second review", Now);

        secondResult.IsSuccess.Should().BeFalse();
        secondResult.Error.Code.Should().Be("review.already_reviewed");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public void Create_WhenInvalidRating_ShouldFail(int rating)
    {
        var booking = CreateTestReviewableBooking();

        var result = Review.Create(booking, CustomerId, rating, "Feedback", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.invalid_rating");
    }

    [Fact]
    public void Create_WhenContentIsEmpty_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();

        var result = Review.Create(booking, CustomerId, 4, "   ", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.empty_content");
    }

    [Fact]
    public void Create_WhenCustomerMismatch_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();

        var result = Review.Create(booking, Guid.NewGuid(), 5, "Good", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.unauthorized");
    }

    [Fact]
    public void Create_WhenCustomerIdIsEmpty_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();

        var result = Review.Create(booking, Guid.Empty, 5, "Good", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.unauthorized");
    }

    [Fact]
    public void HideAndRestore_ByAdmin_ShouldChangeStatusAccordingly()
    {
        var booking = CreateTestReviewableBooking();
        var review = Review.Create(booking, CustomerId, 4, "Normal", Now).Value;
        var adminId = Guid.NewGuid();

        var hideResult = review.Hide(adminId, "Inappropriate language");
        hideResult.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Hidden);
        review.ModerationReason.Should().Be("Inappropriate language");
        review.ModeratedBy.Should().Be(adminId);

        var restoreResult = review.Restore(adminId);
        restoreResult.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(ReviewStatus.Published);
        review.ModerationReason.Should().BeNull();
    }

    [Fact]
    public void Hide_WithEmptyAdminIdOrReason_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();
        var review = Review.Create(booking, CustomerId, 4, "Normal", Now).Value;

        var emptyAdmin = review.Hide(Guid.Empty, "Reason");
        emptyAdmin.IsSuccess.Should().BeFalse();
        emptyAdmin.Error.Code.Should().Be("review.invalid_admin");

        var emptyReason = review.Hide(Guid.NewGuid(), "   ");
        emptyReason.IsSuccess.Should().BeFalse();
        emptyReason.Error.Code.Should().Be("review.missing_reason");
    }

    [Fact]
    public void Restore_WithEmptyAdminId_ShouldFail()
    {
        var booking = CreateTestReviewableBooking();
        var review = Review.Create(booking, CustomerId, 4, "Normal", Now).Value;

        var result = review.Restore(Guid.Empty);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("review.invalid_admin");
    }

    // =========================================================================
    // RatingSummary Aggregate Tests
    // =========================================================================

    [Fact]
    public void RatingSummary_AddAndRemoveRating_ShouldComputeAccurateAverage()
    {
        var summary = RatingSummary.Create(VehicleId);
        summary.TotalReviews.Should().Be(0);
        summary.AverageRating.Should().Be(0m);

        summary.AddRating(5);
        summary.TotalReviews.Should().Be(1);
        summary.AverageRating.Should().Be(5.00m);

        summary.AddRating(3);
        summary.TotalReviews.Should().Be(2);
        summary.AverageRating.Should().Be(4.00m);

        summary.AddRating(4);
        summary.TotalReviews.Should().Be(3);
        summary.AverageRating.Should().Be(4.00m);

        // Remove a 5 rating
        summary.RemoveRating(5);
        summary.TotalReviews.Should().Be(2);
        summary.AverageRating.Should().Be(3.50m);
    }

    [Fact]
    public void RatingSummary_RemoveLastRating_ShouldResetToZero()
    {
        var summary = RatingSummary.Create(VehicleId);
        summary.AddRating(4);
        summary.TotalReviews.Should().Be(1);
        summary.AverageRating.Should().Be(4.00m);

        summary.RemoveRating(4);
        summary.TotalReviews.Should().Be(0);
        summary.AverageRating.Should().Be(0m);
    }

    [Fact]
    public void RatingSummary_AddRating_ShouldClampValuesOutsideRange()
    {
        var summary = RatingSummary.Create(VehicleId);

        // Adding 10 should be clamped to 5
        summary.AddRating(10);
        summary.AverageRating.Should().Be(5.00m);

        // Adding 0 should be clamped to 1
        summary.AddRating(0);
        summary.AverageRating.Should().Be(3.00m); // (5 + 1) / 2
    }
}
