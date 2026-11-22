using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Audit.Contracts.Commands;
using CarRental.Modules.Audit.Entities;
using CarRental.Modules.Audit.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
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
    public void Record_WithEmptyId_ShouldFail()
    {
        var result = AuditLogEntry.Record(
            Guid.Empty,
            Now,
            ActorId,
            "192.168.1.1",
            "UpdateVehicle",
            "Vehicle",
            "veh_999");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("audit.empty_event_id");
    }

    [Fact]
    public void Record_WithEmptyIpAddress_ShouldDefaultToFallback()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "   ",
            "UpdateVehicle",
            "Vehicle",
            "veh_999");

        result.IsSuccess.Should().BeTrue();
        result.Value.IpAddress.Should().Be("0.0.0.0");
    }

    [Fact]
    public void Record_WithNullStates_ShouldSucceed()
    {
        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "127.0.0.1",
            "DeleteDocument",
            "Document",
            "doc_123",
            beforeState: null,
            afterState: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.BeforeState.Should().BeNull();
        result.Value.AfterState.Should().BeNull();
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
        result.Error.Code.Should().Be("audit.empty_actor_id");
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
        result.Error.Code.Should().Be("audit.empty_action");
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
        result.Error.Code.Should().Be("audit.empty_entity_name");
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
        result.Error.Code.Should().Be("audit.empty_entity_id");
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

    [Fact]
    public void Record_WithPiiShorterThan8Digits_ShouldNotMask()
    {
        var json = "{\"postalCode\":\"700000\",\"shortCode\":\"12345\"}";

        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "10.0.0.1",
            "UpdateLocation",
            "Location",
            "loc_1",
            json,
            null);

        result.IsSuccess.Should().BeTrue();
        result.Value.BeforeState.Should().Be(json);
    }

    [Fact]
    public void Record_WithMultipleNestedPii_ShouldMaskAllOccurrences()
    {
        var complexJson = @"{
            ""user"": {
                ""cccd"": ""079090001234"",
                ""gplx"": ""510000123456""
            },
            ""oldDoc"": ""12345678""
        }";

        var result = AuditLogEntry.Record(
            Guid.NewGuid(),
            Now,
            ActorId,
            "10.0.0.1",
            "BatchVerify",
            "User",
            "usr_all",
            complexJson,
            complexJson);

        result.IsSuccess.Should().BeTrue();
        result.Value.BeforeState.Should().NotContain("079090001234");
        result.Value.BeforeState.Should().Contain("*********234");
        result.Value.BeforeState.Should().NotContain("510000123456");
        result.Value.BeforeState.Should().Contain("*********456");
        result.Value.BeforeState.Should().NotContain("12345678");
        result.Value.BeforeState.Should().Contain("*****678");
    }

    [Fact]
    public async Task AuditRecorder_RecordAsync_WithExplicitParameters_ShouldPublishCommand()
    {
        var busMock = new Mock<IMessageBus>();
        var recorder = new AuditRecorder(
            busMock.Object,
            TimeProvider.System,
            NullLogger<AuditRecorder>.Instance);

        var actor = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        await recorder.RecordAsync(
            action: "BanUser",
            entityName: "User",
            entityId: "usr_100",
            beforeState: new { Status = "Active" },
            afterState: new { Status = "Banned" },
            actorId: actor,
            ipAddress: "192.168.1.1",
            eventId: eventId);

        busMock.Verify(b => b.PublishAsync(
            It.Is<RecordAuditEntry>(cmd =>
                cmd.EventId == eventId &&
                cmd.ActorId == actor &&
                cmd.IpAddress == "192.168.1.1" &&
                cmd.Action == "BanUser" &&
                cmd.EntityName == "User" &&
                cmd.EntityId == "usr_100" &&
                cmd.BeforeStateJson != null &&
                cmd.AfterStateJson != null),
            It.IsAny<DeliveryOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task AuditRecorder_RecordAsync_WithHttpContext_ShouldExtractActorAndIp()
    {
        var busMock = new Mock<IMessageBus>();
        var actor = Guid.NewGuid();

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.5");
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actor.ToString())
        }, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.Setup(h => h.HttpContext).Returns(httpContext);

        var recorder = new AuditRecorder(
            busMock.Object,
            TimeProvider.System,
            NullLogger<AuditRecorder>.Instance,
            httpContextAccessorMock.Object);

        await recorder.RecordAsync(
            action: "UnbanUser",
            entityName: "User",
            entityId: "usr_200");

        busMock.Verify(b => b.PublishAsync(
            It.Is<RecordAuditEntry>(cmd =>
                cmd.ActorId == actor &&
                cmd.IpAddress == "10.0.0.5" &&
                cmd.Action == "UnbanUser" &&
                cmd.EntityName == "User" &&
                cmd.EntityId == "usr_200"),
            It.IsAny<DeliveryOptions?>()),
            Times.Once);
    }

    [Fact]
    public async Task AuditRecorder_RecordAsync_WithoutActor_ShouldThrowUnauthorizedAccessException()
    {
        var busMock = new Mock<IMessageBus>();
        var recorder = new AuditRecorder(
            busMock.Object,
            TimeProvider.System,
            NullLogger<AuditRecorder>.Instance);

        var act = () => recorder.RecordAsync(
            action: "BanUser",
            entityName: "User",
            entityId: "usr_100");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AuditRecorderExtensions_ExecuteWithAuditAsync_WhenSuccess_ShouldRecordAudit()
    {
        var recorderMock = new Mock<IAuditRecorder>();
        var actor = Guid.NewGuid();

        var result = await recorderMock.Object.ExecuteWithAuditAsync(
            action: "ApproveVehicle",
            entityName: "Vehicle",
            entityId: "veh_1",
            execute: () => Task.FromResult(Result.Success()),
            captureBeforeState: () => Task.FromResult<object?>(new { Status = "Submitted" }),
            captureAfterState: () => new { Status = "Approved" },
            actorId: actor);

        result.IsSuccess.Should().BeTrue();
        recorderMock.Verify(r => r.RecordAsync(
            "ApproveVehicle",
            "Vehicle",
            "veh_1",
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            actor,
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuditRecorderExtensions_ExecuteWithAuditAsync_WhenFailure_ShouldNotRecordAudit()
    {
        var recorderMock = new Mock<IAuditRecorder>();
        var actor = Guid.NewGuid();

        var result = await recorderMock.Object.ExecuteWithAuditAsync(
            action: "ApproveVehicle",
            entityName: "Vehicle",
            entityId: "veh_1",
            execute: () => Task.FromResult(Result.Failure(Error.NotFound("Vehicle.NotFound", "Vehicle not found"))),
            captureBeforeState: () => Task.FromResult<object?>(null),
            captureAfterState: () => null,
            actorId: actor);

        result.IsFailure.Should().BeTrue();
        recorderMock.Verify(r => r.RecordAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void AuditedAction_RecordCreation_ShouldSetExpectedProperties()
    {
        var eventId = Guid.NewGuid();
        var action = new AuditedAction(
            "BanUser",
            "User",
            "usr_1",
            IncludeBeforeState: true,
            IncludeAfterState: true,
            EventId: eventId);

        action.Action.Should().Be("BanUser");
        action.EntityName.Should().Be("User");
        action.EntityId.Should().Be("usr_1");
        action.IncludeBeforeState.Should().BeTrue();
        action.IncludeAfterState.Should().BeTrue();
        action.EventId.Should().Be(eventId);
    }
}
