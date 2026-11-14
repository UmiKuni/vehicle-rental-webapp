using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Reviews.Contracts.Enums;
using CarRental.Modules.Reviews.Data;
using CarRental.Modules.Reviews.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Reviews;

public class ReviewsIntegrationTests : IntegrationTestBase
{
    public ReviewsIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task Review_CreateAndPersist_ShouldSaveReviewAndMarkBookingReviewed()
    {
        var bookingId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();

            var booking = new ReviewableBooking(bookingId, vehicleId, customerId, now);
            db.ReviewableBookings.Add(booking);

            var review = Review.Create(booking, customerId, 5, "Car was in pristine condition!", now).Value;
            db.Reviews.Add(review);

            var summary = RatingSummary.Create(vehicleId);
            summary.AddRating(5);
            db.RatingSummaries.Add(summary);

            await db.SaveChangesAsync();
        }

        // Verify in PostgreSQL
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();

            var booking = await db.ReviewableBookings.FirstAsync(b => b.BookingId == bookingId);
            booking.HasReviewed.Should().BeTrue();

            var review = await db.Reviews.FirstOrDefaultAsync(r => r.BookingId == bookingId);
            review.Should().NotBeNull();
            review!.Rating.Should().Be(5);
            review.Content.Should().Be("Car was in pristine condition!");
            review.Status.Should().Be(ReviewStatus.Published);

            var summary = await db.RatingSummaries.FirstAsync(s => s.Id == vehicleId);
            summary.TotalReviews.Should().Be(1);
            summary.AverageRating.Should().Be(5.00m);
        }
    }

    [Fact]
    public async Task Review_AdminModeration_ShouldPersistHiddenAndRestoredStates()
    {
        var bookingId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid reviewId;

        // 1. Create review
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
            var booking = new ReviewableBooking(bookingId, vehicleId, customerId, now);
            db.ReviewableBookings.Add(booking);

            var review = Review.Create(booking, customerId, 1, "Offensive content", now).Value;
            db.Reviews.Add(review);
            await db.SaveChangesAsync();
            reviewId = review.Id.Value;
        }

        // 2. Hide review
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
            var review = await db.Reviews.FirstAsync(r => r.Id == ReviewId.From(reviewId));
            review.Hide(adminId, "Violates community guidelines");
            await db.SaveChangesAsync();
        }

        // 3. Verify Hidden in DB
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
            var review = await db.Reviews.FirstAsync(r => r.Id == ReviewId.From(reviewId));
            review.Status.Should().Be(ReviewStatus.Hidden);
            review.ModerationReason.Should().Be("Violates community guidelines");
            review.ModeratedBy.Should().Be(adminId);

            // 4. Restore
            review.Restore(adminId);
            await db.SaveChangesAsync();
        }

        // 5. Verify Restored in DB
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ReviewsDbContext>();
            var review = await db.Reviews.FirstAsync(r => r.Id == ReviewId.From(reviewId));
            review.Status.Should().Be(ReviewStatus.Published);
            review.ModerationReason.Should().BeNull();
        }
    }
}
