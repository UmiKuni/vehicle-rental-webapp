using CarRental.Modules.Audit.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class AuditModuleTests
{
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Record_WithValidParameters_ShouldSucceed()
    {
        var entryId = Guid.NewGuid();
        var result = AuditLogEntry.Record(
            entryId,
            Now,
            ActorId,
            "192.168.1.100",
            "BanUser",
            "User",
            "user_456",
            "{\"status\":\"Active\"}",
            "{\"status\":\"Banned\",\"reason\":\"Fraud\"}");

        result.IsSuccess.Should().BeTrue();
        var entry = result.Value;
        entry.Id.Should().Be(entryId);
        entry.Timestamp.Should().Be(Now);
        entry.ActorId.Should().Be(ActorId);
        entry.IpAddress.Should().Be("192.168.1.100");
        entry.Action.Should().Be("BanUser");
        entry.EntityName.Should().Be("User");
        entry.EntityId.Should().Be("user_456");
        entry.BeforeState.Should().Be("{\"status\":\"Active\"}");
        entry.AfterState.Should().Be("{\"status\":\"Banned\",\"reason\":\"Fraud\"}");
    }

    [Fact]
    public void Record_WithEmptyActorId_ShouldFail()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            Guid.Empty,
            "127.0.0.1",
            "ApproveVehicle",
            "Vehicle",
            "veh_123");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Audit.EmptyActorId");
    }

    [Fact]
    public void Record_WithEmptyAction_ShouldFail()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "127.0.0.1",
            "   ",
            "Vehicle",
            "veh_123");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Audit.EmptyAction");
    }

    [Fact]
    public void Record_WithEmptyEntityName_ShouldFail()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "127.0.0.1",
            "ApproveVehicle",
            "",
            "veh_123");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Audit.EmptyEntityName");
    }

    [Fact]
    public void Record_WithEmptyEntityId_ShouldFail()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "127.0.0.1",
            "ApproveVehicle",
            "Vehicle",
            "   ");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Audit.EmptyEntityId");
    }

    [Fact]
    public void Record_WithSensitivePiiInPayload_ShouldMaskCccdOrGplx()
    {
        var sensitiveJson = "{\"identityDocNumber\":\"012345678901\",\"driverLicense\":\"123456789012\"}";

        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "10.0.0.1",
            "VerifyIdentityDocs",
            "UserIdentityDocument",
            "doc_789",
            null,
            sensitiveJson);

        result.IsSuccess.Should().BeTrue();
        var entry = result.Value;
        entry.AfterState.Should().NotContain("012345678901");
        entry.AfterState.Should().NotContain("123456789012");
        entry.AfterState.Should().Contain("*********901");
        entry.AfterState.Should().Contain("*********012");
    }
}
