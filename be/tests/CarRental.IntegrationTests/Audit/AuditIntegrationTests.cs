using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Audit.Contracts.Commands;
using CarRental.Modules.Audit.Data;
using CarRental.Modules.Audit.Data.Repositories;
using CarRental.Modules.Audit.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Audit;

public class AuditIntegrationTests : IntegrationTestBase
{
    public AuditIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task RecordAuditEntry_ViaBus_ShouldPersistSanitizedAuditLog()
    {
        var eventId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var timestamp = DateTimeOffset.UtcNow;
        var sensitiveAfterJson = @"{
            ""userId"": ""12345"",
            ""cccd"": ""079090001234"",
            ""gplx"": ""510000123456""
        }";

        var command = new RecordAuditEntry(
            eventId,
            timestamp,
            actorId,
            "10.0.0.1",
            "VerifyCustomerDocs",
            "User",
            "usr_999",
            BeforeStateJson: null,
            AfterStateJson: sensitiveAfterJson);

        // 1. Dispatch through Wolverine
        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(command);
        }

        // 2. Verify directly in PostgreSQL audit.audit_logs
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var entry = await db.AuditLogEntries.FirstOrDefaultAsync(e => e.Id == eventId);

            entry.Should().NotBeNull();
            entry!.ActorId.Should().Be(actorId);
            entry.Action.Should().Be("VerifyCustomerDocs");
            entry.EntityName.Should().Be("User");
            entry.EntityId.Should().Be("usr_999");
            entry.IpAddress.Should().Be("10.0.0.1");

            // Verify PII sanitization in database column
            entry.AfterState.Should().NotContain("079090001234");
            entry.AfterState.Should().NotContain("510000123456");
            entry.AfterState.Should().Contain("*********234");
            entry.AfterState.Should().Contain("*********456");
        }
    }

    [Fact]
    public async Task RecordAuditEntry_DuplicateCommand_ShouldBeIdempotent()
    {
        var eventId = Guid.NewGuid();
        var command = new RecordAuditEntry(
            eventId,
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "127.0.0.1",
            "BanUser",
            "User",
            "usr_123",
            null,
            null);

        // Invoke twice
        using (var scope = CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(command);
            await bus.InvokeAsync(command);
        }

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            var count = await db.AuditLogEntries.CountAsync(e => e.Id == eventId);
            count.Should().Be(1);
        }
    }

    [Fact]
    public async Task AuditLogRepository_QueryByEntity_ShouldReturnOrderedHistory()
    {
        var entityId = $"veh_{Guid.NewGuid():N}";
        var actorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();

            var entry1 = AuditLogEntry.Record(Guid.NewGuid(), now.AddMinutes(-10), actorId, "1.1.1.1", "Create", "Vehicle", entityId).Value;
            var entry2 = AuditLogEntry.Record(Guid.NewGuid(), now, actorId, "1.1.1.1", "Publish", "Vehicle", entityId).Value;

            await repo.AddAsync(entry1);
            await repo.AddAsync(entry2);
            await db.SaveChangesAsync();
        }

        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var history = await repo.GetByEntityAsync("Vehicle", entityId);

            history.Should().HaveCount(2);
            history.First().Action.Should().Be("Publish"); // Ordered descending by timestamp
            history.Last().Action.Should().Be("Create");
        }
    }
}
