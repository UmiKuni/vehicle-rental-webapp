using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Bookings.Data;

public class BookingDbContext : DbContext
{
    public const string Schema = "booking";

    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingLineItem> BookingLineItems => Set<BookingLineItem>();
    public DbSet<CancellationPolicySnapshot> CancellationPolicySnapshots => Set<CancellationPolicySnapshot>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();

    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
    }
}
