using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Fleet.Enums;
using CarRental.Modules.Fleet.ValueObjects;

namespace CarRental.Modules.Fleet.Tests;

public class VehicleTests
{
    private readonly DateTimeOffset _now = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    private static Address CreateValidAddress() =>
        new("123 Nguyen Hue", "Ben Nghe", "District 1", "Ho Chi Minh City", "VN", new GeoPoint(10.7769, 106.7009));

    private static FileReference CreatePublicPhoto(string key) =>
        new(key, $"https://res.cloudinary.com/demo/{key}", isPrivate: false, "image/jpeg", 2048);

    private static FileReference CreatePrivateDoc(string key) =>
        new(key, null, isPrivate: true, "application/pdf", 4096);

    private Vehicle CreateDraftVehicle()
    {
        var result = Vehicle.Create(
            VehicleId.New(),
            Guid.NewGuid(),
            "Toyota",
            "Camry",
            2022,
            new LicensePlate("51K-123.45"),
            5,
            TransmissionType.Automatic,
            FuelType.Petrol,
            "Toyota Camry 2022 luxury sedan",
            "Excellent condition, well maintained.",
            CreateValidAddress(),
            new VehicleRules(21, 1, false, "Same-to-same", "No smoking"),
            new[] { VehicleFeature.AirConditioning, VehicleFeature.Bluetooth, VehicleFeature.ReverseCamera });

        return result.Value;
    }

    [Fact]
    public void Submit_WithFewerThan5Photos_ShouldFail()
    {
        var vehicle = CreateDraftVehicle();

        // Add 4 photos
        for (int i = 0; i < 4; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: i == 0);
        }

        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), DateOnly.FromDateTime(_now.AddYears(1).DateTime));

        var result = vehicle.Submit(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("vehicle.insufficient_photos");
        vehicle.Status.Should().Be(VehicleStatus.Draft);
    }

    [Fact]
    public void Submit_WithNoPrimaryPhoto_ShouldFail()
    {
        var vehicle = CreateDraftVehicle();

        // Add 5 photos without primary
        for (int i = 0; i < 5; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: false);
        }

        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), DateOnly.FromDateTime(_now.AddYears(1).DateTime));

        var result = vehicle.Submit(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("vehicle.invalid_primary_photo");
    }

    [Fact]
    public void Submit_WithExpiredInsurance_ShouldFail()
    {
        var vehicle = CreateDraftVehicle();

        for (int i = 0; i < 5; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: i == 0);
        }

        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        // Expired yesterday
        var expiredDate = DateOnly.FromDateTime(_now.AddDays(-1).DateTime);
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), expiredDate);

        var result = vehicle.Submit(_now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("vehicle.invalid_insurance");
    }

    [Fact]
    public void Approve_FromPendingApproval_ShouldTransitionToActive()
    {
        var vehicle = CreateDraftVehicle();

        for (int i = 0; i < 5; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: i == 0);
        }

        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), DateOnly.FromDateTime(_now.AddYears(1).DateTime));

        var submitResult = vehicle.Submit(_now);
        submitResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.PendingApproval);

        var adminId = Guid.NewGuid();
        var approveResult = vehicle.Approve(adminId, _now);

        approveResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Active);
        vehicle.ApprovedBy.Should().Be(adminId);
        vehicle.ApprovedAt.Should().Be(_now);

        vehicle.DomainEvents.Should().ContainSingle(e => e is VehicleApproved);
        vehicle.DomainEvents.Should().ContainSingle(e => e is VehicleListingChanged);
    }

    [Fact]
    public void Pause_FromActive_ShouldTransitionToPaused()
    {
        var vehicle = CreateDraftVehicle();
        for (int i = 0; i < 5; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: i == 0);
        }
        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), DateOnly.FromDateTime(_now.AddYears(1).DateTime));
        vehicle.Submit(_now);
        vehicle.Approve(Guid.NewGuid(), _now);
        vehicle.ClearDomainEvents();

        var pauseResult = vehicle.Pause(_now);

        pauseResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Paused);
        vehicle.DomainEvents.Should().ContainSingle(e => e is VehicleStatusChanged && ((VehicleStatusChanged)e).NewStatus == "Paused");
    }

    [Fact]
    public void Resume_FromPaused_ShouldTransitionToActive()
    {
        var vehicle = CreateDraftVehicle();
        for (int i = 0; i < 5; i++)
        {
            vehicle.AddPhoto(CreatePublicPhoto($"photo_{i}.jpg"), isPrimary: i == 0);
        }
        vehicle.AddDocument(VehicleDocumentType.Registration, CreatePrivateDoc("reg.pdf"));
        vehicle.AddDocument(VehicleDocumentType.Insurance, CreatePrivateDoc("ins.pdf"), DateOnly.FromDateTime(_now.AddYears(1).DateTime));
        vehicle.Submit(_now);
        vehicle.Approve(Guid.NewGuid(), _now);
        vehicle.Pause(_now);
        vehicle.ClearDomainEvents();

        var resumeResult = vehicle.Resume(_now);

        resumeResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Active);
        vehicle.DomainEvents.Should().ContainSingle(e => e is VehicleStatusChanged && ((VehicleStatusChanged)e).NewStatus == "Active");
    }

    [Fact]
    public void Archive_ShouldBecomeTerminalState()
    {
        var vehicle = CreateDraftVehicle();

        var archiveResult = vehicle.Archive(_now);

        archiveResult.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(VehicleStatus.Archived);

        // Cannot add photo after archive
        var addPhotoResult = vehicle.AddPhoto(CreatePublicPhoto("test.jpg"));
        addPhotoResult.IsFailure.Should().BeTrue();
        addPhotoResult.Error.Code.Should().Be("vehicle.archived");

        // Cannot archive again
        var secondArchive = vehicle.Archive(_now);
        secondArchive.IsFailure.Should().BeTrue();
        secondArchive.Error.Code.Should().Be("vehicle.already_archived");
    }
}
