using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class HandoverModuleTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly Guid BookingId = Guid.NewGuid();
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static HandoverContext CreateTestContext(bool isActive = true)
    {
        var ctx = new HandoverContext(BookingId, VehicleId, CustomerId, OwnerId, Now, Now.AddDays(3));
        if (!isActive)
        {
            ctx.Deactivate();
        }
        return ctx;
    }

    private static void AddAllMandatoryPhotos(HandoverEntity handover)
    {
        handover.AddPhoto(PhotoCorner.FrontLeft, "photos/fl.jpg", "HASH_FL", Now);
        handover.AddPhoto(PhotoCorner.FrontRight, "photos/fr.jpg", "HASH_FR", Now);
        handover.AddPhoto(PhotoCorner.RearLeft, "photos/rl.jpg", "HASH_RL", Now);
        handover.AddPhoto(PhotoCorner.RearRight, "photos/rr.jpg", "HASH_RR", Now);
        handover.AddPhoto(PhotoCorner.Dash, "photos/dash.jpg", "HASH_DASH", Now);
    }

    [Fact]
    public void Start_WithActiveContext_ShouldReturnDraftHandover()
    {
        var ctx = CreateTestContext();

        var result = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now);

        result.IsSuccess.Should().BeTrue();
        var handover = result.Value;
        handover.Status.Should().Be(HandoverStatus.Draft);
        handover.BookingId.Should().Be(BookingId);
        handover.Type.Should().Be(HandoverType.Pickup);
        handover.Photos.Should().BeEmpty();
    }

    [Fact]
    public void Start_WhenContextInactive_ShouldFail()
    {
        var ctx = CreateTestContext(isActive: false);

        var result = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("handover.inactive_context");
    }

    [Fact]
    public void AddPhoto_WithoutFileHash_ShouldFail()
    {
        var ctx = CreateTestContext();
        var handover = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now).Value;

        var result = handover.AddPhoto(PhotoCorner.FrontLeft, "photos/fl.jpg", "", Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("handover.missing_hash");
    }

    [Fact]
    public void SetCondition_WithInvalidFuel_ShouldFail()
    {
        var ctx = CreateTestContext();
        var handover = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now).Value;

        var result = handover.SetCondition(15000, 105, "Scratches on door");

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("handover.invalid_fuel");
    }

    [Fact]
    public void Confirm_WhenMissingMandatoryPhotos_ShouldFail()
    {
        var ctx = CreateTestContext();
        var handover = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now).Value;
        handover.SetCondition(15000, 80, null);
        handover.AddPhoto(PhotoCorner.FrontLeft, "photos/fl.jpg", "HASH_FL", Now);

        var result = handover.Confirm(CustomerId, CustomerId, OwnerId, Now);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("handover.missing_mandatory_photo");
    }

    [Fact]
    public void Confirm_WhenDualSigned_ShouldTransitionToConfirmedAndBecomeImmutable()
    {
        var ctx = CreateTestContext();
        var handover = HandoverEntity.Start(ctx, HandoverType.Pickup, CustomerId, Now).Value;
        handover.SetCondition(15000, 80, null);
        AddAllMandatoryPhotos(handover);

        // First confirmation by customer
        var customerConfirm = handover.Confirm(CustomerId, CustomerId, OwnerId, Now);
        customerConfirm.IsSuccess.Should().BeTrue();
        handover.Status.Should().Be(HandoverStatus.Draft); // Still Draft until both confirm

        // Second confirmation by owner
        var ownerConfirm = handover.Confirm(OwnerId, CustomerId, OwnerId, Now.AddMinutes(5));
        ownerConfirm.IsSuccess.Should().BeTrue();
        handover.Status.Should().Be(HandoverStatus.Confirmed);

        // Attempting to modify after Confirmed should fail
        var postPhoto = handover.AddPhoto(PhotoCorner.Dash, "photos/dash2.jpg", "HASH_NEW", Now);
        postPhoto.IsSuccess.Should().BeFalse();
        postPhoto.Error.Code.Should().Be("handover.immutable");
    }

    [Fact]
    public void Confirm_Return_WhenOdometerDecreased_ShouldFail()
    {
        var ctx = CreateTestContext();
        var handover = HandoverEntity.Start(ctx, HandoverType.Return, CustomerId, Now).Value;
        handover.SetCondition(14900, 80, null); // Less than pickup (15000)
        AddAllMandatoryPhotos(handover);

        var result = handover.Confirm(CustomerId, CustomerId, OwnerId, Now, pickupOdometer: 15000);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("handover.odometer_decreased");
    }
}
