using CarRental.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Notifications.Data;

public class NotificationsDbContext : DbContext
{
    public const string Schema = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
