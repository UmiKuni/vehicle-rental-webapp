using CarRental.Modules.Availability.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Availability.Data;

public class AvailabilityDbContext : DbContext
{
    public const string Schema = "availability";

    public DbSet<VehicleCalendar> VehicleCalendars => Set<VehicleCalendar>();
    public DbSet<CalendarEntry> CalendarEntries => Set<CalendarEntry>();

    public AvailabilityDbContext(DbContextOptions<AvailabilityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AvailabilityDbContext).Assembly);
    }
}
