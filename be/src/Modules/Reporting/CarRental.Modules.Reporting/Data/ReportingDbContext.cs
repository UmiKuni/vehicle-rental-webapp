using CarRental.Modules.Reporting.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Reporting.Data;

public class ReportingDbContext : DbContext
{
    public const string Schema = "reporting";

    public DbSet<DailyBookingStats> DailyBookingStats => Set<DailyBookingStats>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
    }
}
