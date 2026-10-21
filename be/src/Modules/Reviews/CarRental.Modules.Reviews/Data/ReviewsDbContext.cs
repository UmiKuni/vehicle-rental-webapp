using CarRental.Modules.Reviews.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Reviews.Data;

public class ReviewsDbContext : DbContext
{
    public const string Schema = "reviews";

    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<RatingSummary> RatingSummaries => Set<RatingSummary>();
    public DbSet<ReviewableBooking> ReviewableBookings => Set<ReviewableBooking>();

    public ReviewsDbContext(DbContextOptions<ReviewsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReviewsDbContext).Assembly);
    }
}
