using System.Reflection;
using System.Security.Cryptography;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Storage;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Dtos;
using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Handover.Data;
using CarRental.Modules.Handover.Data.Repositories;
using CarRental.Modules.Handover.Entities;
using CarRental.Modules.Handover.Services;
using CarRental.Modules.Handover.Validators;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;

namespace CarRental.Modules.Tests.Handover;

public class HandoverApiServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HandoverErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(HandoverEntity).Assembly.GetType("CarRental.Modules.Handover.Errors.HandoverErrors");
        errorsType.Should().NotBeNull();

        var fieldErrors = errorsType!
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Error))
            .Select(f => (Name: f.Name, Error: (Error)f.GetValue(null)!));

        var propertyErrors = errorsType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Error))
            .Select(p => (Name: p.Name, Error: (Error)p.GetValue(null)!));

        var methodErrors = errorsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => !m.IsSpecialName && m.ReturnType == typeof(Error))
            .Select(m =>
            {
                var args = m.GetParameters().Select(p =>
                    p.ParameterType == typeof(int) ? (object)100 :
                    p.ParameterType == typeof(PhotoCorner) ? PhotoCorner.Dash :
                    throw new InvalidOperationException($"Unsupported param type {p.ParameterType}")).ToArray();
                return (Name: m.Name, Error: (Error)m.Invoke(null, args)!);
            });

        var allErrors = fieldErrors.Concat(propertyErrors).Concat(methodErrors).ToList();
        allErrors.Should().HaveCount(20);

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex("^handover\\.[a-z_]+$", $"Error '{name}' must use handover.* snake_case naming.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();
        allErrors.Select(e => e.Name).Should().NotContain(n =>
            n.Contains("5CEF8A") || n.Contains("E2911A") || n.Contains("B8F729") || n.Contains("A1098D"));

        var notParticipant = allErrors.Single(e => e.Name == "NotParticipant").Error;
        notParticipant.Code.Should().Be("handover.not_participant");
        notParticipant.Type.Should().Be(ErrorType.Forbidden);

        var immutable = allErrors.Single(e => e.Name == "Immutable").Error;
        immutable.Code.Should().Be("handover.immutable");
        immutable.Type.Should().Be(ErrorType.Conflict);

        var notFound = allErrors.Single(e => e.Name == "NotFound").Error;
        notFound.Code.Should().Be("handover.not_found");
        notFound.Type.Should().Be(ErrorType.NotFound);

        var roleMismatch = allErrors.Single(e => e.Name == "RoleMismatch").Error;
        roleMismatch.Code.Should().Be("handover.role_mismatch");
        roleMismatch.Type.Should().Be(ErrorType.Forbidden);

        var notReady = allErrors.Single(e => e.Name == "BookingNotReadyForPickup").Error;
        notReady.Code.Should().Be("handover.booking_not_ready_for_pickup");
        notReady.Type.Should().Be(ErrorType.Conflict);

        var notInProgress = allErrors.Single(e => e.Name == "BookingNotInProgress").Error;
        notInProgress.Code.Should().Be("handover.booking_not_in_progress");
        notInProgress.Type.Should().Be(ErrorType.Conflict);

        var alreadyExists = allErrors.Single(e => e.Name == "AlreadyExistsForType").Error;
        alreadyExists.Code.Should().Be("handover.already_exists");
        alreadyExists.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_ShouldValidateTypeParticipantBookingStatusAndDuplicates()
    {
        var harness = CreateHarness();
        var bookingId = harness.BookingId;

        var invalidType = await harness.Service.CreateAsync(harness.CustomerId, new CreateHandoverRequest(bookingId, "Invalid"));
        invalidType.IsFailure.Should().BeTrue();
        invalidType.Error.Code.Should().Be("handover.invalid_type");

        var nonParticipant = await harness.Service.CreateAsync(Guid.NewGuid(), new CreateHandoverRequest(bookingId, "Pickup"));
        nonParticipant.IsFailure.Should().BeTrue();
        nonParticipant.Error.Code.Should().Be("handover.not_participant");
        nonParticipant.Error.Type.Should().Be(ErrorType.Forbidden);

        // Return when booking is still Paid -> Conflict
        var returnTooEarly = await harness.Service.CreateAsync(harness.CustomerId, new CreateHandoverRequest(bookingId, "Return"));
        returnTooEarly.IsFailure.Should().BeTrue();
        returnTooEarly.Error.Code.Should().Be("handover.booking_not_in_progress");
        returnTooEarly.Error.Type.Should().Be(ErrorType.Conflict);

        // Pickup when booking is Paid -> Success
        var pickupResult = await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(bookingId, "Pickup"));
        pickupResult.IsSuccess.Should().BeTrue();
        pickupResult.Value.Type.Should().Be("Pickup");
        pickupResult.Value.Status.Should().Be("Draft");
        harness.Contexts.Should().ContainKey(bookingId);

        // Duplicate Pickup -> Conflict
        var duplicatePickup = await harness.Service.CreateAsync(harness.CustomerId, new CreateHandoverRequest(bookingId, "Pickup"));
        duplicatePickup.IsFailure.Should().BeTrue();
        duplicatePickup.Error.Code.Should().Be("handover.already_exists");
        duplicatePickup.Error.Type.Should().Be(ErrorType.Conflict);

        // Pickup when booking is InProgress -> Conflict
        harness.SetBookingStatus("InProgress");
        var booking2Id = Guid.NewGuid();
        harness.BookingApiMock
            .Setup(x => x.GetRentalDetailsAsync(booking2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new BookingRentalDetailsDto(
                booking2Id, harness.CustomerId, harness.OwnerId, harness.VehicleId,
                "InProgress", Now, Now.AddDays(2), 1_000_000, 2_000_000)));
        harness.BookingApiMock
            .Setup(x => x.IsParticipantAsync(booking2Id, It.Is<Guid>(id => id == harness.CustomerId || id == harness.OwnerId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var pickupWhenInProgress = await harness.Service.CreateAsync(harness.CustomerId, new CreateHandoverRequest(booking2Id, "Pickup"));
        pickupWhenInProgress.IsFailure.Should().BeTrue();
        pickupWhenInProgress.Error.Code.Should().Be("handover.booking_not_ready_for_pickup");
    }

    [Fact]
    public async Task UpdateConditionAndPhotos_ShouldAllowMultipleUpdatesAndCornerReplacement()
    {
        var harness = CreateHarness();
        var handover = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Pickup"))).Value;

        // First condition update
        var cond1 = await harness.Service.UpdateConditionAsync(handover.Id, harness.OwnerId, new UpdateVehicleConditionRequest(10_000, 90, "Clean"));
        cond1.IsSuccess.Should().BeTrue();
        cond1.Value.Odo.Should().Be(10_000);

        // Second condition update
        var cond2 = await harness.Service.UpdateConditionAsync(handover.Id, harness.CustomerId, new UpdateVehicleConditionRequest(10_050, 85, "Small scratch"));
        cond2.IsSuccess.Should().BeTrue();
        cond2.Value.Odo.Should().Be(10_050);
        cond2.Value.FuelLevel.Should().Be(85);
        cond2.Value.DamageNote.Should().Be("Small scratch");

        // Invalid photo corner
        var invalidPhoto = await harness.Service.AddPhotoAsync(
            handover.Id,
            harness.CustomerId,
            new UploadHandoverPhotoRequest("Roof", "photos/roof.jpg", "HASH", Now));
        invalidPhoto.IsFailure.Should().BeTrue();
        invalidPhoto.Error.Code.Should().Be("handover.invalid_corner");

        // Upload FrontLeft then replace FrontLeft
        var photo1 = await harness.Service.AddPhotoAsync(
            handover.Id,
            harness.CustomerId,
            new UploadHandoverPhotoRequest("FrontLeft", "photos/fl1.jpg", "HASH1", Now));
        photo1.IsSuccess.Should().BeTrue();
        photo1.Value.ViewUrl.Should().StartWith("https://storage.carrental.vn/private/photos/fl1.jpg?exp=")
            .And.Contain("&sig=");

        var photo2 = await harness.Service.AddPhotoAsync(
            handover.Id,
            harness.OwnerId,
            new UploadHandoverPhotoRequest("FrontLeft", "photos/fl2.jpg", "HASH2", Now.AddMinutes(1)));
        photo2.IsSuccess.Should().BeTrue();
        photo2.Value.StorageKey.Should().Be("photos/fl2.jpg");

        var detail = await harness.Service.GetByIdAsync(handover.Id, harness.CustomerId);
        detail.IsSuccess.Should().BeTrue();
        detail.Value.Photos.Should().ContainSingle(p => p.Corner == "FrontLeft" && p.StorageKey == "photos/fl2.jpg");
    }

    [Fact]
    public async Task UploadPhotoFileAsync_ShouldUploadToStorageComputeSha256AndReturnSignedPrivateUrl()
    {
        var harness = CreateHarness();
        var handover = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Pickup"))).Value;

        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x11, 0x22, 0x33, 0x44 };
        var expectedHash = Convert.ToHexStringLower(SHA256.HashData(imageBytes));

        await using var stream = new MemoryStream(imageBytes);
        var uploadResult = await harness.Service.UploadPhotoFileAsync(
            handover.Id,
            harness.CustomerId,
            new UploadHandoverPhotoFileRequest("FrontRight", stream, "front-right.jpg", "image/jpeg", Now));

        uploadResult.IsSuccess.Should().BeTrue();
        uploadResult.Value.Corner.Should().Be("FrontRight");
        uploadResult.Value.StorageKey.Should().StartWith($"handover/{harness.BookingId:N}/");
        uploadResult.Value.StorageKey.Should().EndWith(".jpg");
        uploadResult.Value.FileHash.Should().Be(expectedHash);
        uploadResult.Value.ViewUrl.Should().StartWith($"https://storage.carrental.vn/private/{uploadResult.Value.StorageKey}?exp=")
            .And.Contain("&sig=");
        harness.ImageStorage.Contains(uploadResult.Value.StorageKey).Should().BeTrue();

        var replacementBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x55, 0x66, 0x77, 0x88 };
        await using var replacementStream = new MemoryStream(replacementBytes);
        var replacementResult = await harness.Service.UploadPhotoFileAsync(
            handover.Id,
            harness.CustomerId,
            new UploadHandoverPhotoFileRequest("FrontRight", replacementStream, "front-right-2.jpg", "image/jpeg", Now));
        replacementResult.IsSuccess.Should().BeTrue();
        harness.ImageStorage.Contains(uploadResult.Value.StorageKey).Should().BeFalse("replaced corner photo should be deleted from storage");
        harness.ImageStorage.Contains(replacementResult.Value.StorageKey).Should().BeTrue();

        await using var invalidStream = new MemoryStream([1, 2, 3]);
        var invalidType = await harness.Service.UploadPhotoFileAsync(
            handover.Id,
            harness.CustomerId,
            new UploadHandoverPhotoFileRequest("Dash", invalidStream, "doc.pdf", "application/pdf", Now));
        invalidType.IsFailure.Should().BeTrue();
        invalidType.Error.Code.Should().Be("storage.unsupported_content_type");
    }

    [Fact]
    public async Task GetByBookingAndById_ShouldEnforceParticipantOrAdmin()
    {
        var harness = CreateHarness();
        var handover = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Pickup"))).Value;
        var outsiderId = Guid.NewGuid();

        var outsiderList = await harness.Service.GetByBookingAsync(harness.BookingId, outsiderId, isAdmin: false);
        outsiderList.IsFailure.Should().BeTrue();
        outsiderList.Error.Code.Should().Be("handover.not_participant");

        var adminList = await harness.Service.GetByBookingAsync(harness.BookingId, outsiderId, isAdmin: true);
        adminList.IsSuccess.Should().BeTrue();
        adminList.Value.Should().ContainSingle(h => h.Id == handover.Id);

        var outsiderDetail = await harness.Service.GetByIdAsync(handover.Id, outsiderId, isAdmin: false);
        outsiderDetail.IsFailure.Should().BeTrue();
        outsiderDetail.Error.Code.Should().Be("handover.not_participant");

        var adminDetail = await harness.Service.GetByIdAsync(handover.Id, outsiderId, isAdmin: true);
        adminDetail.IsSuccess.Should().BeTrue();

        var missingDetail = await harness.Service.GetByIdAsync(Guid.NewGuid(), harness.CustomerId);
        missingDetail.IsFailure.Should().BeTrue();
        missingDetail.Error.Code.Should().Be("handover.not_found");
    }

    [Fact]
    public async Task ConfirmAsync_PickupAndReturn_ShouldTriggerTripLifecycleAndCalculateOverKmFee()
    {
        // Booking is 2 days (48h). KmLimit = 200 km/day => allowed = 400 km. OverKm = 5,000 VND/km.
        var harness = CreateHarness(kmLimitPerDay: 200, overKmFeePerKm: 5_000L);

        // 1. Pickup flow
        var pickup = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Pickup"))).Value;
        await harness.Service.UpdateConditionAsync(pickup.Id, harness.OwnerId, new UpdateVehicleConditionRequest(10_000, 100, null));
        await UploadAllMandatoryPhotosAsync(harness.Service, pickup.Id, harness.OwnerId);

        // Invalid role & role mismatch
        var invalidRole = await harness.Service.ConfirmAsync(pickup.Id, harness.CustomerId, new ConfirmHandoverRequest("Admin", null));
        invalidRole.IsFailure.Should().BeTrue();
        invalidRole.Error.Code.Should().Be("handover.invalid_role");

        var roleMismatch = await harness.Service.ConfirmAsync(pickup.Id, harness.CustomerId, new ConfirmHandoverRequest("Owner", null));
        roleMismatch.IsFailure.Should().BeTrue();
        roleMismatch.Error.Code.Should().Be("handover.role_mismatch");

        // Customer confirms first -> still Draft, StartTripAsync NOT called yet
        var pickupCustConfirm = await harness.Service.ConfirmAsync(pickup.Id, harness.CustomerId, new ConfirmHandoverRequest("Customer", "sig-c"));
        pickupCustConfirm.IsSuccess.Should().BeTrue();
        pickupCustConfirm.Value.Status.Should().Be("Draft");
        pickupCustConfirm.Value.IsConfirmedByCustomer.Should().BeTrue();
        pickupCustConfirm.Value.IsConfirmedByOwner.Should().BeFalse();
        pickupCustConfirm.Value.FinalizedAt.Should().BeNull();
        harness.BookingApiMock.Verify(x => x.StartTripAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.OutboxMock.Verify(x => x.PublishAsync(It.IsAny<HandoverConfirmed>(), It.IsAny<CancellationToken>()), Times.Never);

        // Owner confirms second -> Confirmed, StartTripAsync called, HandoverConfirmed published
        harness.AdvanceTime(TimeSpan.FromMinutes(5));
        var pickupOwnerConfirm = await harness.Service.ConfirmAsync(pickup.Id, harness.OwnerId, new ConfirmHandoverRequest("Owner", "sig-o"));
        pickupOwnerConfirm.IsSuccess.Should().BeTrue();
        pickupOwnerConfirm.Value.Status.Should().Be("Confirmed");
        pickupOwnerConfirm.Value.IsConfirmedByOwner.Should().BeTrue();
        pickupOwnerConfirm.Value.FinalizedAt.Should().NotBeNull();
        harness.BookingApiMock.Verify(x => x.StartTripAsync(harness.BookingId, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<HandoverConfirmed>(e =>
                    e.BookingId == harness.BookingId &&
                    e.Type == "Pickup" &&
                    e.Odometer == 10_000 &&
                    e.FuelLevel == 100 &&
                    e.DistanceKm == null),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // 2. Return flow with 550 km driven (allowed 400 km -> 150 over km * 5,000 = 750,000 VND)
        harness.SetBookingStatus("InProgress");
        var ret = (await harness.Service.CreateAsync(harness.CustomerId, new CreateHandoverRequest(harness.BookingId, "Return"))).Value;
        await harness.Service.UpdateConditionAsync(ret.Id, harness.CustomerId, new UpdateVehicleConditionRequest(10_550, 80, "No new damage"));
        await UploadAllMandatoryPhotosAsync(harness.Service, ret.Id, harness.CustomerId);

        await harness.Service.ConfirmAsync(ret.Id, harness.OwnerId, new ConfirmHandoverRequest("Owner", null));
        var retCustConfirm = await harness.Service.ConfirmAsync(ret.Id, harness.CustomerId, new ConfirmHandoverRequest("Customer", null));

        retCustConfirm.IsSuccess.Should().BeTrue();
        retCustConfirm.Value.Status.Should().Be("Confirmed");
        harness.BookingApiMock.Verify(
            x => x.CompleteTripAsync(harness.BookingId, 750_000m, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<HandoverConfirmed>(e =>
                    e.BookingId == harness.BookingId &&
                    e.Type == "Return" &&
                    e.Odometer == 10_550 &&
                    e.FuelLevel == 80 &&
                    e.DistanceKm == 550),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConfirmAsync_ReturnWithinKmLimit_ShouldPassNullOverKmFee()
    {
        var harness = CreateHarness(kmLimitPerDay: 200, overKmFeePerKm: 5_000L);

        var pickup = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Pickup"))).Value;
        await harness.Service.UpdateConditionAsync(pickup.Id, harness.OwnerId, new UpdateVehicleConditionRequest(10_000, 100, null));
        await UploadAllMandatoryPhotosAsync(harness.Service, pickup.Id, harness.OwnerId);
        await harness.Service.ConfirmAsync(pickup.Id, harness.CustomerId, new ConfirmHandoverRequest("Customer", null));
        await harness.Service.ConfirmAsync(pickup.Id, harness.OwnerId, new ConfirmHandoverRequest("Owner", null));

        harness.SetBookingStatus("InProgress");
        var ret = (await harness.Service.CreateAsync(harness.OwnerId, new CreateHandoverRequest(harness.BookingId, "Return"))).Value;
        await harness.Service.UpdateConditionAsync(ret.Id, harness.OwnerId, new UpdateVehicleConditionRequest(10_300, 90, null));
        await UploadAllMandatoryPhotosAsync(harness.Service, ret.Id, harness.OwnerId);
        await harness.Service.ConfirmAsync(ret.Id, harness.CustomerId, new ConfirmHandoverRequest("Customer", null));
        var retConfirm = await harness.Service.ConfirmAsync(ret.Id, harness.OwnerId, new ConfirmHandoverRequest("Owner", null));

        retConfirm.IsSuccess.Should().BeTrue();
        harness.BookingApiMock.Verify(
            x => x.CompleteTripAsync(harness.BookingId, null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void RequestValidators_ShouldRejectInvalidInputsWithConformingErrorCodes()
    {
        var createValidator = new CreateHandoverRequestValidator();
        var badBooking = createValidator.Validate(new CreateHandoverRequest(Guid.Empty, "Pickup"));
        badBooking.IsValid.Should().BeFalse();
        badBooking.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_context");

        var badType = createValidator.Validate(new CreateHandoverRequest(Guid.NewGuid(), "Invalid"));
        badType.IsValid.Should().BeFalse();
        badType.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_type");

        var numericType = createValidator.Validate(new CreateHandoverRequest(Guid.NewGuid(), "1"));
        numericType.IsValid.Should().BeFalse();
        numericType.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_type");

        createValidator.Validate(new CreateHandoverRequest(Guid.NewGuid(), "Pickup")).IsValid.Should().BeTrue();

        var conditionValidator = new UpdateVehicleConditionRequestValidator();
        var badCondition = conditionValidator.Validate(new UpdateVehicleConditionRequest(-1, 105, null));
        badCondition.IsValid.Should().BeFalse();
        badCondition.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_odometer");
        badCondition.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_fuel");

        conditionValidator.Validate(new UpdateVehicleConditionRequest(0, 100, "Clean")).IsValid.Should().BeTrue();

        var photoValidator = new UploadHandoverPhotoRequestValidator();
        var badPhoto = photoValidator.Validate(new UploadHandoverPhotoRequest("Roof", " ", "", Now));
        badPhoto.IsValid.Should().BeFalse();
        badPhoto.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_corner");
        badPhoto.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_photo");
        badPhoto.Errors.Should().Contain(e => e.ErrorCode == "handover.missing_hash");

        photoValidator.Validate(new UploadHandoverPhotoRequest("FrontLeft", "photos/fl.jpg", "HASH", Now)).IsValid.Should().BeTrue();

        var confirmValidator = new ConfirmHandoverRequestValidator();
        var badConfirm = confirmValidator.Validate(new ConfirmHandoverRequest("Admin", null));
        badConfirm.IsValid.Should().BeFalse();
        badConfirm.Errors.Should().Contain(e => e.ErrorCode == "handover.invalid_role");

        confirmValidator.Validate(new ConfirmHandoverRequest("Customer", "sig-c")).IsValid.Should().BeTrue();
        confirmValidator.Validate(new ConfirmHandoverRequest("Owner", null)).IsValid.Should().BeTrue();
    }

    private static async Task UploadAllMandatoryPhotosAsync(HandoverApiService service, Guid handoverId, Guid actorId)
    {
        foreach (var corner in Enum.GetValues<PhotoCorner>())
        {
            var result = await service.AddPhotoAsync(
                handoverId,
                actorId,
                new UploadHandoverPhotoRequest(corner.ToString(), $"photos/{corner}.jpg", $"HASH_{corner}", Now));
            result.IsSuccess.Should().BeTrue();
        }
    }

    private sealed class TestHarness
    {
        public required HandoverApiService Service { get; init; }
        public required InMemoryImageStorage ImageStorage { get; init; }
        public required Mock<IBookingApi> BookingApiMock { get; init; }
        public required Mock<IEventOutbox> OutboxMock { get; init; }
        public required Dictionary<Guid, HandoverContext> Contexts { get; init; }
        public required Guid BookingId { get; init; }
        public required Guid VehicleId { get; init; }
        public required Guid CustomerId { get; init; }
        public required Guid OwnerId { get; init; }
        public required Action<TimeSpan> AdvanceTime { get; init; }
        public required Action<string> SetBookingStatus { get; init; }
    }

    private static TestHarness CreateHarness(int kmLimitPerDay = 250, long? overKmFeePerKm = 5_000L)
    {
        var currentTime = Now;
        var bookingId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var bookingStatus = "Paid";

        var handovers = new Dictionary<HandoverId, HandoverEntity>();
        var contexts = new Dictionary<Guid, HandoverContext>();

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(() => currentTime);

        var dbContextMock = new Mock<HandoverDbContext>(new DbContextOptionsBuilder<HandoverDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IHandoverRepository>();
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<HandoverId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((HandoverId id, CancellationToken _) => handovers.GetValueOrDefault(id));
        repoMock.Setup(x => x.GetByBookingAndTypeAsync(It.IsAny<Guid>(), It.IsAny<HandoverType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, HandoverType t, CancellationToken _) =>
                handovers.Values.SingleOrDefault(h => h.BookingId == bId && h.Type == t));
        repoMock.Setup(x => x.GetByBookingIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, CancellationToken _) =>
                handovers.Values.Where(h => h.BookingId == bId).OrderBy(h => h.ServerReceivedAt).ToList());
        repoMock.Setup(x => x.GetContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid bId, CancellationToken _) => contexts.GetValueOrDefault(bId));
        repoMock.Setup(x => x.Add(It.IsAny<HandoverEntity>()))
            .Callback((HandoverEntity h) => handovers[h.Id] = h);
        repoMock.Setup(x => x.AddContext(It.IsAny<HandoverContext>()))
            .Callback((HandoverContext c) => contexts[c.BookingId] = c);

        var bookingApiMock = new Mock<IBookingApi>();
        bookingApiMock.Setup(x => x.IsParticipantAsync(bookingId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid actor, CancellationToken _) => actor == customerId || actor == ownerId);
        bookingApiMock.Setup(x => x.GetRentalDetailsAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(new BookingRentalDetailsDto(
                bookingId,
                customerId,
                ownerId,
                vehicleId,
                bookingStatus,
                Now,
                Now.AddDays(2),
                1_200_000m,
                5_000_000m)));
        bookingApiMock.Setup(x => x.StartTripAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        bookingApiMock.Setup(x => x.CompleteTripAsync(It.IsAny<Guid>(), It.IsAny<decimal?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var pricingApiMock = new Mock<IPricingApi>();
        var feeRules = overKmFeePerKm.HasValue
            ? new List<FeeRuleDto> { new("OverKm", new Money(overKmFeePerKm.Value, "VND")) }
            : new List<FeeRuleDto>();
        pricingApiMock.Setup(x => x.GetPricingAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new PricingPlanDto(
                vehicleId,
                null,
                new Money(600_000L, "VND"),
                new Money(800_000L, "VND"),
                new Money(5_000_000L, "VND"),
                kmLimitPerDay,
                feeRules)));

        var outboxMock = new Mock<IEventOutbox>();
        outboxMock.Setup(x => x.PublishAsync(It.IsAny<HandoverConfirmed>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var imageStorage = new InMemoryImageStorage(clockMock.Object);

        var service = new HandoverApiService(
            dbContextMock.Object,
            repoMock.Object,
            bookingApiMock.Object,
            pricingApiMock.Object,
            outboxMock.Object,
            clockMock.Object,
            imageStorage);

        return new TestHarness
        {
            Service = service,
            ImageStorage = imageStorage,
            BookingApiMock = bookingApiMock,
            OutboxMock = outboxMock,
            Contexts = contexts,
            BookingId = bookingId,
            VehicleId = vehicleId,
            CustomerId = customerId,
            OwnerId = ownerId,
            AdvanceTime = delta => currentTime = currentTime.Add(delta),
            SetBookingStatus = status => bookingStatus = status
        };
    }
}
