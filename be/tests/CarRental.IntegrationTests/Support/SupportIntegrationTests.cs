using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Support.Contracts.Enums;
using CarRental.Modules.Support.Data;
using CarRental.Modules.Support.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarRental.IntegrationTests.Support;

public class SupportIntegrationTests : IntegrationTestBase
{
    public SupportIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SupportTicket_OpenAndThreadMessages_ShouldPersistWithChildMessages()
    {
        var bookingId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid ticketId;

        // 1. Open ticket
        var ticket = SupportTicket.Open(
            bookingId,
            reporterId,
            "Vehicle windshield had a crack on arrival",
            "evidence/windshield.jpg",
            now).Value;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id.Value;
        }

        // 2. Add reply message to the thread
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var tracked = await db.SupportTickets.Include(t => t.Messages).FirstAsync(t => t.Id == TicketId.From(ticketId));
            tracked.AddMessage(reporterId, "Attached additional photos showing odometer and license plate", "evidence/plate.jpg", now.AddMinutes(5));
            await db.SaveChangesAsync();
        }

        // 3. Verify ticket and both messages in PostgreSQL
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var persisted = await db.SupportTickets.Include(t => t.Messages).FirstAsync(t => t.Id == TicketId.From(ticketId));

            persisted.BookingId.Should().Be(bookingId);
            persisted.ReporterId.Should().Be(reporterId);
            persisted.Status.Should().Be(TicketStatus.Open);
            persisted.Messages.Should().HaveCount(2);
            persisted.Messages.First().Content.Should().Be("Vehicle windshield had a crack on arrival");
            persisted.Messages.Last().Content.Should().Be("Attached additional photos showing odometer and license plate");
            persisted.Messages.Last().AttachmentKey.Should().Be("evidence/plate.jpg");
        }
    }

    [Fact]
    public async Task SupportTicket_AdminReviewAndResolve_ShouldPersistDisputeSplit()
    {
        var bookingId = Guid.NewGuid();
        var reporterId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Guid ticketId;

        // 1. Open ticket
        var ticket = SupportTicket.Open(bookingId, reporterId, "Deposit dispute for fuel fee", null, now).Value;
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            db.SupportTickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id.Value;
        }

        // 2. Admin starts review
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var tracked = await db.SupportTickets.FirstAsync(t => t.Id == TicketId.From(ticketId));
            tracked.StartReview(adminId, now.AddMinutes(10));
            await db.SaveChangesAsync();
        }

        // 3. Admin resolves with 70% refund to customer
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var tracked = await db.SupportTickets.FirstAsync(t => t.Id == TicketId.From(ticketId));
            tracked.Resolve(adminId, 70, "Owner submitted fuel receipt; 70% refunded to renter.", now.AddHours(1));
            await db.SaveChangesAsync();
        }

        // 4. Verify in PostgreSQL
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupportDbContext>();
            var resolved = await db.SupportTickets.FirstAsync(t => t.Id == TicketId.From(ticketId));

            resolved.Status.Should().Be(TicketStatus.Resolved);
            resolved.DepositToCustomerPercent.Should().Be(70);
            resolved.ResolutionNote.Should().Be("Owner submitted fuel receipt; 70% refunded to renter.");
            resolved.ResolvedBy.Should().Be(adminId);
            resolved.ResolvedAt.Should().NotBeNull();
        }
    }
}
