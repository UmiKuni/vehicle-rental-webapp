using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Contracts.Enums;
using CarRental.Modules.Fleet.Entities;
using FluentAssertions;
using Xunit;

namespace CarRental.Modules.Tests;

public class FleetModuleTests
{
    private static readonly GeoPoint TestLocation = new(10.7769, 106.7009);
    private const string TestAddress = "123 Nguyen Hue, Ben Nghe, District 1, Ho Chi Minh City";

    private static Vehicle CreateTestVehicle()
    {
        return Vehicle.Create(
            Guid.NewGuid(),
            "51K-999.88",
            Guid.NewGuid(),
            Guid.NewGuid(),
            TransmissionType.Auto,
            5,
            TestLocation,
            TestAddress).Value;
    }

    [Fact]
    public void Create_WithValidParameters_ShouldReturnDraftVehicle()
    {
        var vehicle = CreateTestVehicle();

        vehicle.Should().NotBeNull();
        vehicle.PlateNumber.Should().Be("51K-999.88");
        vehicle.Status.Should().Be(VehicleStatus.Draft);
        vehicle.Seats.Should().Be(5);
        vehicle.HiddenByAdmin.Should().BeFalse();
        vehicle.Images.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithInvalidParameters_ShouldFail()
    {
        var emptyOwner = Vehicle.Create(Guid.Empty, "51K-12345", Guid.NewGuid(), Guid.NewGuid(), TransmissionType.Auto, 5, TestLocation, TestAddress);
        emptyOwner.IsSuccess.Should().BeFalse();
        emptyOwner.Error.Code.Should().Be("fleet.invalid_owner");

        var emptyPlate = Vehicle.Create(Guid.NewGuid(), "", Guid.NewGuid(), Guid.NewGuid(), TransmissionType.Auto, 5, TestLocation, TestAddress);
        emptyPlate.IsSuccess.Should().BeFalse();
        emptyPlate.Error.Code.Should().Be("fleet.invalid_plate");

        var zeroSeats = Vehicle.Create(Guid.NewGuid(), "51K-12345", Guid.NewGuid(), Guid.NewGuid(), TransmissionType.Auto, 0, TestLocation, TestAddress);
        zeroSeats.IsSuccess.Should().BeFalse();
        zeroSeats.Error.Code.Should().Be("fleet.invalid_seats");
    }

    [Fact]
    public void AddImage_FirstImage_ShouldBePrimary()
    {
        var vehicle = CreateTestVehicle();

        var result1 = vehicle.AddImage("vehicles/img1.jpg");
        result1.IsSuccess.Should().BeTrue();
        vehicle.Images.Should().HaveCount(1);
        vehicle.Images.First().IsPrimary.Should().BeTrue();

        var result2 = vehicle.AddImage("vehicles/img2.jpg");
        result2.IsSuccess.Should().BeTrue();
        vehicle.Images.Should().HaveCount(2);
        vehicle.Images.Last().IsPrimary.Should().BeFalse();
    }

    [Fact]
    public void SubmitForApproval_WithFewerThan3Images_ShouldFail()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");

        var submitResult = vehicle.SubmitForApproval();

        submitResult.IsSuccess.Should().BeFalse();
        submitResult.Error.Code.Should().Be("fleet.insufficient_images");
    }

    [Fact]
    public void SubmitForApproval_With3Images_ShouldTransitionToPendingApproval()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");
        vehicle.AddImage("vehicles/img3.jpg");

        var submitResult = vehicle.SubmitForApproval();

        submitResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.PendingApproval);
    }

    [Fact]
    public void Approve_FromPendingApproval_ShouldTransitionToPublished()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");
        vehicle.AddImage("vehicles/img3.jpg");
        vehicle.SubmitForApproval();

        var adminId = Guid.NewGuid();
        var approveResult = vehicle.Approve(adminId);

        approveResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Published);
        vehicle.Approvals.Should().HaveCount(1);
        vehicle.Approvals.First().Decision.Should().Be(ApprovalDecision.Approved);
        vehicle.Approvals.First().AdminId.Should().Be(adminId);
    }

    [Fact]
    public void Reject_FromPendingApproval_ShouldTransitionBackToDraft()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");
        vehicle.AddImage("vehicles/img3.jpg");
        vehicle.SubmitForApproval();

        var adminId = Guid.NewGuid();
        var rejectResult = vehicle.Reject(adminId, "Inadequate photos of vehicle interior.");

        rejectResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Draft);
        vehicle.Approvals.Should().HaveCount(1);
        vehicle.Approvals.First().Decision.Should().Be(ApprovalDecision.Rejected);
        vehicle.Approvals.First().Reason.Should().Contain("interior");
    }

    [Fact]
    public void Unpublish_ByAdmin_ShouldHideAndRequireReapproval()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");
        vehicle.AddImage("vehicles/img3.jpg");
        vehicle.SubmitForApproval();
        vehicle.Approve(Guid.NewGuid());

        var adminId = Guid.NewGuid();
        var unpublishResult = vehicle.Unpublish(adminId, "Safety violation");

        unpublishResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Hidden);
        vehicle.HiddenByAdmin.Should().BeTrue();

        // Owner trying to unhide should fail
        var showResult = vehicle.Show();
        showResult.IsSuccess.Should().BeFalse();
        showResult.Error.Code.Should().Be("fleet.hidden_by_admin");
    }

    [Fact]
    public void Hide_And_Show_ByOwner_ShouldWorkWhenNotHiddenByAdmin()
    {
        var vehicle = CreateTestVehicle();
        vehicle.AddImage("vehicles/img1.jpg");
        vehicle.AddImage("vehicles/img2.jpg");
        vehicle.AddImage("vehicles/img3.jpg");
        vehicle.SubmitForApproval();
        vehicle.Approve(Guid.NewGuid());

        var hideResult = vehicle.Hide();
        hideResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Hidden);

        var showResult = vehicle.Show();
        showResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Published);
    }
}
