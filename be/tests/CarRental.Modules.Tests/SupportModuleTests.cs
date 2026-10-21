using CarRental.Modules.Support.Contracts.Enums;
using CarRental.Modules.Support.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class SupportModuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid ReporterId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    [Fact]
    public void Open_WithValidParameters_ShouldSucceedAndHaveInitialMessage()
    {
        var result = SupportTicket.Open(BookingId, ReporterId, "Air conditioning was broken during the rental", "evidence/ac.jpg", Now);

        result.IsSuccess.Should().BeTrue();
        var ticket = result.Value;
        ticket.BookingId.Should().Be(BookingId);
        ticket.ReporterId.Should().Be(ReporterId);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Messages.Should().HaveCount(1);
        ticket.Messages.First().Content.Should().Be("Air conditioning was broken during the rental");
        ticket.Messages.First().AttachmentKey.Should().Be("evidence/ac.jpg");
    }

    [Fact]
    public void Open_WithEmptyMessage_ShouldFail()
    {
        var result = SupportTicket.Open(BookingId, ReporterId, "   ", null, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("support.empty_message");
    }

    [Fact]
    public void AddMessage_ToOpenTicket_ShouldSucceed()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Initial problem", null, Now).Value;

        var addResult = ticket.AddMessage(ReporterId, "Here is additional detail", "evidence/doc.pdf", Now.AddMinutes(10));

        addResult.IsSuccess.Should().BeTrue();
        ticket.Messages.Should().HaveCount(2);
        ticket.Messages.Last().Content.Should().Be("Here is additional detail");
    }

    [Fact]
    public void StartReview_ByAdmin_ShouldTransitionToInProgress()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Need assistance", null, Now).Value;

        var reviewResult = ticket.StartReview(AdminId, Now.AddMinutes(5));

        reviewResult.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.InProgress);
    }

    [Fact]
    public void Resolve_WithValidPercentage_ShouldTransitionToResolved()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Deposit dispute", null, Now).Value;
        ticket.StartReview(AdminId, Now.AddMinutes(5));

        // 80% refund to customer, 20% to owner for cleaning
        var resolveResult = ticket.Resolve(AdminId, 80, "Owner justified cleaning fee; 80% refunded to customer.", Now.AddMinutes(30));

        resolveResult.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.DepositToCustomerPercent.Should().Be(80);
        ticket.ResolutionNote.Should().Be("Owner justified cleaning fee; 80% refunded to customer.");
        ticket.ResolvedBy.Should().Be(AdminId);
        ticket.ResolvedAt.Should().Be(Now.AddMinutes(30));
    }

    [Fact]
    public void Resolve_WithInvalidPercentage_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Deposit dispute", null, Now).Value;

        var belowZero = ticket.Resolve(AdminId, -5, "Note", Now);
        belowZero.IsSuccess.Should().BeFalse();
        belowZero.Error.Code.Should().Be("support.invalid_percent");

        var above100 = ticket.Resolve(AdminId, 105, "Note", Now);
        above100.IsSuccess.Should().BeFalse();
        above100.Error.Code.Should().Be("support.invalid_percent");
    }

    [Fact]
    public void AddMessage_ToResolvedTicket_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Deposit dispute", null, Now).Value;
        ticket.Resolve(AdminId, 100, "Full refund granted", Now);

        var addResult = ticket.AddMessage(ReporterId, "Can I still say something?", null, Now.AddMinutes(5));

        addResult.IsSuccess.Should().BeFalse();
        addResult.Error.Code.Should().Be("support.ticket_resolved");
    }
}
