using CarRental.Modules.Support.Contracts.Enums;
using CarRental.Modules.Support.Contracts.Events;
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
        ticket.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<TicketOpened>();
    }

    [Fact]
    public void Open_WithEmptyBookingId_ShouldFail()
    {
        var result = SupportTicket.Open(Guid.Empty, ReporterId, "Problem description", null, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("support.invalid_booking");
    }

    [Fact]
    public void Open_WithEmptyReporterId_ShouldFail()
    {
        var result = SupportTicket.Open(BookingId, Guid.Empty, "Problem description", null, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("support.invalid_reporter");
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
    public void AddMessage_WithEmptySenderOrContent_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Initial problem", null, Now).Value;

        var emptySender = ticket.AddMessage(Guid.Empty, "Hello", null, Now);
        emptySender.IsSuccess.Should().BeFalse();
        emptySender.Error.Code.Should().Be("support.invalid_sender");

        var emptyContent = ticket.AddMessage(ReporterId, "   ", null, Now);
        emptyContent.IsSuccess.Should().BeFalse();
        emptyContent.Error.Code.Should().Be("support.empty_message");
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
    public void StartReview_WithEmptyAdminId_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Need assistance", null, Now).Value;

        var result = ticket.StartReview(Guid.Empty, Now.AddMinutes(5));

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("support.invalid_admin");
    }

    [Fact]
    public void StartReview_WhenAlreadyInProgress_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Need assistance", null, Now).Value;
        ticket.StartReview(AdminId, Now.AddMinutes(5));

        var secondReview = ticket.StartReview(AdminId, Now.AddMinutes(10));

        secondReview.IsSuccess.Should().BeFalse();
        secondReview.Error.Code.Should().Be("support.invalid_state");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Resolve_BoundaryPercentages_ShouldSucceed(int percent)
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Dispute", null, Now).Value;
        ticket.StartReview(AdminId, Now.AddMinutes(5));

        var result = ticket.Resolve(AdminId, percent, $"Resolution note with {percent}%", Now.AddMinutes(30));

        result.IsSuccess.Should().BeTrue();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.DepositToCustomerPercent.Should().Be(percent);
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
    public void Resolve_WithEmptyAdminIdOrEmptyNote_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Deposit dispute", null, Now).Value;

        var emptyAdmin = ticket.Resolve(Guid.Empty, 50, "Valid note", Now);
        emptyAdmin.IsSuccess.Should().BeFalse();
        emptyAdmin.Error.Code.Should().Be("support.invalid_admin");

        var emptyNote = ticket.Resolve(AdminId, 50, "   ", Now);
        emptyNote.IsSuccess.Should().BeFalse();
        emptyNote.Error.Code.Should().Be("support.missing_resolution_note");
    }

    [Fact]
    public void Resolve_WhenAlreadyResolved_ShouldFail()
    {
        var ticket = SupportTicket.Open(BookingId, ReporterId, "Dispute", null, Now).Value;
        ticket.Resolve(AdminId, 100, "Full refund", Now);

        var secondResolve = ticket.Resolve(AdminId, 50, "Attempt re-resolve", Now);

        secondResolve.IsSuccess.Should().BeFalse();
        secondResolve.Error.Code.Should().Be("support.already_resolved");
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
