using System.Reflection;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Availability.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Bookings.Contracts.Enums;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Bookings.Data;
using CarRental.Modules.Bookings.Data.Repositories;
using CarRental.Modules.Bookings.Entities;
using CarRental.Modules.Bookings.Services;
using CarRental.Modules.Bookings.Validators;
using CarRental.Modules.Fleet.Contracts;
using CarRental.Modules.Fleet.Contracts.Dtos;
using CarRental.Modules.Pricing.Contracts;
using CarRental.Modules.Pricing.Contracts.Dtos;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CarRental.Modules.Tests.Bookings;

public class BookingsApiServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void BookingsErrors_ShouldHaveUniqueConformingCodesAndExpectedTypes()
    {
        var errorsType = typeof(Booking).Assembly.GetType("CarRental.Modules.Bookings.Errors.BookingsErrors");
        errorsType.Should().NotBeNull();

        var propertyErrors = errorsType!
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Error))
            .Select(p => (Name: p.Name, Error: (Error)p.GetValue(null)!))
            .ToList();

        var methodErrors = errorsType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.ReturnType == typeof(Error) && !m.IsSpecialName)
            .Select(m =>
            {
                var args = m.GetParameters().Select(p =>
                    p.ParameterType == typeof(string) ? (object)"Pending" :
                    p.ParameterType == typeof(BookingStatus) ? BookingStatus.Pending :
                    throw new NotSupportedException()).ToArray();
                return (Name: m.Name, Error: (Error)m.Invoke(null, args)!);
            })
            .ToList();

        var allErrors = propertyErrors.Concat(methodErrors).ToList();
        allErrors.Should().NotBeEmpty();

        foreach (var (name, error) in allErrors)
        {
            error.Code.Should().MatchRegex("^booking\\.[a-z_]+$", $"Error '{name}' must use booking.* snake_case naming.");
        }

        allErrors.Select(e => e.Error.Code).Should().OnlyHaveUniqueItems();
        allErrors.Select(e => e.Name).Should().NotContain(n =>
            n.Contains("Unauthorized5E2B0A") || n.Contains("UnauthorizedBDDFB3") || n.Contains("Unauthorized6779FC"));

        allErrors.Single(e => e.Name == "NotOwner").Error.Type.Should().Be(ErrorType.Forbidden);
        allErrors.Single(e => e.Name == "NotCustomer").Error.Type.Should().Be(ErrorType.Forbidden);
        allErrors.Single(e => e.Name == "NotParticipant").Error.Type.Should().Be(ErrorType.Forbidden);
        allErrors.Single(e => e.Name == "TripInProgress").Error.Type.Should().Be(ErrorType.Conflict);
        allErrors.Single(e => e.Name == "OverKmFeeRequiresCompleted").Error.Type.Should().Be(ErrorType.Conflict);
        allErrors.Single(e => e.Name == "VehicleNotPublished").Error.Type.Should().Be(ErrorType.Conflict);
        allErrors.Single(e => e.Name == "NotFound").Error.Type.Should().Be(ErrorType.NotFound);
        allErrors.Single(e => e.Name == "ReasonRequired").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "InvalidStatusFilter").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "CustomerNotEligible").Error.Type.Should().Be(ErrorType.Validation);
        allErrors.Single(e => e.Name == "InvalidState").Error.Type.Should().Be(ErrorType.Conflict);
        allErrors.Single(e => e.Name == "TerminalState").Error.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_WhenValid_ShouldRequestBookingPlaceHoldSaveAndPublishEvent()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var start = Now.AddDays(2);
        var end = Now.AddDays(5);

        var quote = new QuoteDto(
            vehicleId,
            Money.Of(2_650_000),
            Money.Of(5_000_000),
            300,
            Money.Of(5_000),
            [
                new QuoteLineDto("BaseRate", "Rent (3 days)", Money.Of(2_550_000)),
                new QuoteLineDto("CleaningFee", "Cleaning fee", Money.Of(100_000)),
                new QuoteLineDto("Deposit", "Security deposit", Money.Of(5_000_000))
            ],
            new CancellationPolicyDto([
                new CancellationTierDto(48, 100m),
                new CancellationTierDto(24, 70m),
                new CancellationTierDto(0, 0m)
            ]));

        harness.PricingMock
            .Setup(x => x.GetQuoteAsync(vehicleId, It.IsAny<DateTimeRange>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(quote));

        var result = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, start, end, "123 Nguyen Hue, HCMC", false, null));

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Status.Should().Be(nameof(BookingStatus.Pending));
        dto.CustomerId.Should().Be(customerId);
        dto.OwnerId.Should().Be(harness.OwnerId);
        dto.VehicleId.Should().Be(vehicleId);
        dto.VehiclePlate.Should().Be("51A-12345");
        dto.VehicleModel.Should().Be("Toyota Camry");
        dto.CustomerName.Should().Be("Nguyen Van A");
        dto.CustomerPhone.Should().Be("0901234567");
        dto.TotalRentAmount.Should().Be(2_650_000m);
        dto.SecurityDepositAmount.Should().Be(5_000_000m);
        dto.HoldExpiresAt.Should().Be(Now.AddHours(12));
        dto.LineItems.Should().Contain(l => l.Type == "Rent" && l.Amount == 2_550_000m);
        dto.LineItems.Should().Contain(l => l.Type == "CleaningFee" && l.Amount == 100_000m);
        dto.LineItems.Should().Contain(l => l.Type == "Deposit" && l.Amount == 5_000_000m);

        var storedBooking = harness.Bookings.Values.Single();
        storedBooking.PolicySnapshot.Should().NotBeNull();
        storedBooking.PolicySnapshot!.PolicyDetailsJson.Should().Contain("\"PenaltyPercent\":30");
        storedBooking.PolicySnapshot.PolicyDetailsJson.Should().Contain("\"PenaltyPercent\":0");

        harness.AvailabilityMock.Verify(
            x => x.PlaceHoldAsync(vehicleId, dto.Id, It.IsAny<DateTimeRange>(), TimeSpan.FromHours(12), It.IsAny<CancellationToken>()),
            Times.Once);
        harness.DbContextMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<BookingRequested>(e =>
                    e.BookingId == dto.Id
                    && e.CustomerId == customerId
                    && e.VehicleId == vehicleId
                    && e.Total.Amount == 2_650_000
                    && e.DepositAmount.Amount == 5_000_000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomerNotEligibleOrVehicleNotPublished_ShouldFailWithoutPlacingHold()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        harness.UsersMock
            .Setup(x => x.GetBookingEligibilityAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new BookingEligibilityDto(false, "CCCD is not verified.")));

        var ineligibleResult = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));

        ineligibleResult.IsFailure.Should().BeTrue();
        ineligibleResult.Error.Code.Should().Be("booking.customer_not_eligible");
        ineligibleResult.Error.Type.Should().Be(ErrorType.Validation);

        harness.UsersMock
            .Setup(x => x.GetBookingEligibilityAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new BookingEligibilityDto(true, null)));
        harness.FleetMock
            .Setup(x => x.GetVehicleSummaryAsync(vehicleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new VehicleSummaryDto(
                vehicleId, harness.OwnerId, "Draft", Guid.NewGuid(), "Toyota", Guid.NewGuid(), "Vios",
                "51A-99999", "Auto", 5, null, GeoPoint.Create(10.77, 106.7).Value, "HCMC")));

        var unpublishedResult = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));

        unpublishedResult.IsFailure.Should().BeTrue();
        unpublishedResult.Error.Code.Should().Be("booking.vehicle_not_published");
        unpublishedResult.Error.Type.Should().Be(ErrorType.Conflict);

        harness.AvailabilityMock.Verify(
            x => x.PlaceHoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeRange>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSaveChangesFails_ShouldReleaseHoldAsCompensation()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        harness.DbContextMock
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Simulated DB failure"));

        var act = () => harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));

        await act.Should().ThrowAsync<DbUpdateException>();
        harness.AvailabilityMock.Verify(
            x => x.ReleaseHoldAsync(vehicleId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_And_RejectAsync_ShouldTransitionPublishAndReleaseHold()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var created = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(3), Now.AddDays(5), "HCMC", false, null));
        created.IsSuccess.Should().BeTrue();

        var approveResult = await harness.Service.ApproveAsync(created.Value.Id, harness.OwnerId);
        approveResult.IsSuccess.Should().BeTrue();
        approveResult.Value.Status.Should().Be(nameof(BookingStatus.Confirmed));
        approveResult.Value.HoldExpiresAt.Should().Be(Now.AddHours(2));

        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<BookingConfirmed>(e =>
                    e.BookingId == created.Value.Id
                    && e.PayableAmount.Amount == 7_000_000
                    && e.DepositPortion.Amount == 5_000_000
                    && e.PaymentDeadline == Now.AddHours(2)),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Second booking for reject test
        var created2 = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(6), Now.AddDays(8), "HCMC", false, null));
        created2.IsSuccess.Should().BeTrue();

        var emptyReasonReject = await harness.Service.RejectAsync(created2.Value.Id, harness.OwnerId, "   ");
        emptyReasonReject.IsFailure.Should().BeTrue();
        emptyReasonReject.Error.Code.Should().Be("booking.reason_required");

        var rejectResult = await harness.Service.RejectAsync(created2.Value.Id, harness.OwnerId, "Vehicle scheduled for service");
        rejectResult.IsSuccess.Should().BeTrue();
        rejectResult.Value.Status.Should().Be(nameof(BookingStatus.Rejected));

        harness.AvailabilityMock.Verify(
            x => x.ReleaseHoldAsync(vehicleId, created2.Value.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<BookingRejected>(e =>
                    e.BookingId == created2.Value.Id
                    && e.OwnerId == harness.OwnerId
                    && e.Reason == "Vehicle scheduled for service"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WhenPaidWithPolicy_ShouldCalculatePenaltyRefundReleaseHoldAndPublishEvent()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        // Start is 30 hours after Now -> matches 24h tier (RefundPercent = 70 => PenaltyPercent = 30)
        var start = Now.AddHours(30);
        var end = start.AddDays(2);

        harness.PricingMock
            .Setup(x => x.GetQuoteAsync(vehicleId, It.IsAny<DateTimeRange>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new QuoteDto(
                vehicleId,
                Money.Of(2_000_000),
                Money.Of(5_000_000),
                300,
                null,
                [
                    new QuoteLineDto("BaseRate", "Rent", Money.Of(2_000_000)),
                    new QuoteLineDto("Deposit", "Deposit", Money.Of(5_000_000))
                ],
                new CancellationPolicyDto([
                    new CancellationTierDto(48, 100m),
                    new CancellationTierDto(24, 70m),
                    new CancellationTierDto(0, 0m)
                ]))));

        var created = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, start, end, "HCMC", false, null));
        created.IsSuccess.Should().BeTrue();

        (await harness.Service.ApproveAsync(created.Value.Id, harness.OwnerId)).IsSuccess.Should().BeTrue();
        (await harness.Service.MarkPaidAsync(created.Value.Id, Guid.NewGuid(), Now)).IsSuccess.Should().BeTrue();

        var cancelResult = await harness.Service.CancelAsync(created.Value.Id, customerId, "Change of plans");
        cancelResult.IsSuccess.Should().BeTrue();
        cancelResult.Value.Status.Should().Be(nameof(BookingStatus.Cancelled));
        cancelResult.Value.TotalPaid.Should().Be(7_000_000m);
        cancelResult.Value.PenaltyAmount.Should().Be(600_000m); // 30% of 2,000,000
        cancelResult.Value.RefundAmount.Should().Be(6_400_000m); // 1,400,000 + 5,000,000

        harness.AvailabilityMock.Verify(
            x => x.ReleaseHoldAsync(vehicleId, created.Value.Id, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(
                It.Is<BookingCancelled>(e =>
                    e.BookingId == created.Value.Id
                    && e.PenaltyAmount.Amount == 600_000
                    && e.RefundableAmount.Amount == 6_400_000),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MarkPaid_StartTrip_And_CompleteTrip_ShouldTransitionAndPublishEvents()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var created = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));
        var bookingId = created.Value.Id;

        (await harness.Service.ApproveAsync(bookingId, harness.OwnerId)).IsSuccess.Should().BeTrue();

        var paidAt = Now.AddMinutes(15);
        (await harness.Service.MarkPaidAsync(bookingId, Guid.NewGuid(), paidAt)).IsSuccess.Should().BeTrue();
        harness.AvailabilityMock.Verify(
            x => x.ConfirmHoldAsync(vehicleId, bookingId, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.OutboxMock.Verify(
            x => x.PublishAsync(It.Is<BookingPaid>(e => e.BookingId == bookingId), It.IsAny<CancellationToken>()),
            Times.Once);

        (await harness.Service.StartTripAsync(bookingId, Now.AddDays(2))).IsSuccess.Should().BeTrue();
        harness.Bookings[bookingId].Status.Should().Be(BookingStatus.InProgress);

        (await harness.Service.CompleteTripAsync(bookingId, 150_000m, Now.AddDays(4))).IsSuccess.Should().BeTrue();
        harness.Bookings[bookingId].Status.Should().Be(BookingStatus.Completed);
        harness.Bookings[bookingId].LineItems.Should().Contain(l =>
            l.Type == BookingLineItemType.OverKmFee && l.Amount.Amount == 150_000);

        harness.OutboxMock.Verify(
            x => x.PublishAsync(It.Is<BookingCompleted>(e => e.BookingId == bookingId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMyBookings_And_GetById_ShouldValidateFilterAndParticipantAccess()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var created = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));

        var invalidFilter = await harness.Service.GetMyBookingsAsync(customerId, "NotAStatus");
        invalidFilter.IsFailure.Should().BeTrue();
        invalidFilter.Error.Code.Should().Be("booking.invalid_status_filter");

        var myBookings = await harness.Service.GetMyBookingsAsync(customerId, "Pending", page: 1, pageSize: 10);
        myBookings.IsSuccess.Should().BeTrue();
        myBookings.Value.Items.Should().ContainSingle(b => b.Id == created.Value.Id);

        var strangerResult = await harness.Service.GetByIdAsync(created.Value.Id, Guid.NewGuid(), isAdmin: false);
        strangerResult.IsFailure.Should().BeTrue();
        strangerResult.Error.Code.Should().Be("booking.not_participant");
        strangerResult.Error.Type.Should().Be(ErrorType.Forbidden);

        var adminResult = await harness.Service.GetByIdAsync(created.Value.Id, Guid.NewGuid(), isAdmin: true);
        adminResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CancelAsync_And_AdminCancelAsync_WhenReasonIsBlank_ShouldReturnReasonRequired()
    {
        var harness = CreateHarness();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var created = await harness.Service.CreateAsync(
            customerId,
            new CreateBookingRequest(vehicleId, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));
        created.IsSuccess.Should().BeTrue();

        var cancelResult = await harness.Service.CancelAsync(created.Value.Id, customerId, "   ");
        cancelResult.IsFailure.Should().BeTrue();
        cancelResult.Error.Code.Should().Be("booking.reason_required");

        var adminCancelResult = await harness.Service.AdminCancelAsync(created.Value.Id, Guid.NewGuid(), "   ", "Customer");
        adminCancelResult.IsFailure.Should().BeTrue();
        adminCancelResult.Error.Code.Should().Be("booking.reason_required");
    }

    [Fact]
    public void RequestValidators_ShouldRejectInvalidInputsWithConformingErrorCodes()
    {
        var createValidator = new CreateBookingRequestValidator();
        var validCreate = createValidator.Validate(new CreateBookingRequest(
            Guid.NewGuid(), Now.AddDays(2), Now.AddDays(4), "123 Nguyen Hue, HCMC", false, null));
        validCreate.IsValid.Should().BeTrue();

        var badVehicle = createValidator.Validate(new CreateBookingRequest(
            Guid.Empty, Now.AddDays(2), Now.AddDays(4), "HCMC", false, null));
        badVehicle.IsValid.Should().BeFalse();
        badVehicle.Errors.Should().Contain(e => e.ErrorCode == "booking.invalid_vehicle");

        var badPeriod = createValidator.Validate(new CreateBookingRequest(
            Guid.NewGuid(), default, default, "HCMC", false, null));
        badPeriod.IsValid.Should().BeFalse();
        badPeriod.Errors.Should().Contain(e => e.ErrorCode == "booking.invalid_period");

        var badDuration = createValidator.Validate(new CreateBookingRequest(
            Guid.NewGuid(), Now.AddDays(4), Now.AddDays(2), "HCMC", false, null));
        badDuration.IsValid.Should().BeFalse();
        badDuration.Errors.Should().Contain(e => e.ErrorCode == "booking.invalid_duration");

        var badLocation = createValidator.Validate(new CreateBookingRequest(
            Guid.NewGuid(), Now.AddDays(2), Now.AddDays(4), "   ", false, null));
        badLocation.IsValid.Should().BeFalse();
        badLocation.Errors.Should().Contain(e => e.ErrorCode == "booking.invalid_location");

        var rejectValidator = new RejectBookingRequestValidator();
        rejectValidator.Validate(new RejectBookingRequest("Valid reason")).IsValid.Should().BeTrue();
        var badReject = rejectValidator.Validate(new RejectBookingRequest("   "));
        badReject.IsValid.Should().BeFalse();
        badReject.Errors.Should().Contain(e => e.ErrorCode == "booking.reason_required");

        var cancelValidator = new CancelBookingRequestValidator();
        cancelValidator.Validate(new CancelBookingRequest("Schedule changed")).IsValid.Should().BeTrue();
        var badCancel = cancelValidator.Validate(new CancelBookingRequest(""));
        badCancel.IsValid.Should().BeFalse();
        badCancel.Errors.Should().Contain(e => e.ErrorCode == "booking.reason_required");

        var adminCancelValidator = new AdminCancelBookingRequestValidator();
        adminCancelValidator.Validate(new AdminCancelBookingRequest("Policy violation", string.Empty)).IsValid.Should().BeTrue();
        var badAdminCancel = adminCancelValidator.Validate(new AdminCancelBookingRequest("  ", "Owner"));
        badAdminCancel.IsValid.Should().BeFalse();
        badAdminCancel.Errors.Should().Contain(e => e.ErrorCode == "booking.reason_required");
    }

    private static (
        BookingApiService Service,
        Guid OwnerId,
        Dictionary<Guid, Booking> Bookings,
        Mock<BookingDbContext> DbContextMock,
        Mock<IUsersApi> UsersMock,
        Mock<IFleetApi> FleetMock,
        Mock<IPricingApi> PricingMock,
        Mock<IAvailabilityApi> AvailabilityMock,
        Mock<IEventOutbox> OutboxMock) CreateHarness()
    {
        var ownerId = Guid.NewGuid();
        var bookings = new Dictionary<Guid, Booking>();

        var clockMock = new Mock<TimeProvider>();
        clockMock.Setup(x => x.GetUtcNow()).Returns(Now);

        var dbContextMock = new Mock<BookingDbContext>(new DbContextOptionsBuilder<BookingDbContext>().Options);
        dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var repoMock = new Mock<IBookingRepository>();
        repoMock.Setup(x => x.Add(It.IsAny<Booking>()))
            .Callback((Booking b) => bookings[b.Id.Value] = b);
        repoMock.Setup(x => x.GetByIdAsync(It.IsAny<BookingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingId id, CancellationToken _) => bookings.GetValueOrDefault(id.Value));
        repoMock.Setup(x => x.GetByIdReadOnlyAsync(It.IsAny<BookingId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookingId id, CancellationToken _) => bookings.GetValueOrDefault(id.Value));
        repoMock.Setup(x => x.GetPagedByCustomerAsync(
                It.IsAny<Guid>(), It.IsAny<BookingStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid customerId, BookingStatus? status, int page, int pageSize, CancellationToken _) =>
            {
                var filtered = bookings.Values
                    .Where(b => b.CustomerId == customerId && (!status.HasValue || b.Status == status.Value))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();
                var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return ((IReadOnlyList<Booking>)paged, filtered.Count);
            });
        repoMock.Setup(x => x.GetPagedByOwnerAsync(
                It.IsAny<Guid>(), It.IsAny<BookingStatus?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid oId, BookingStatus? status, int page, int pageSize, CancellationToken _) =>
            {
                var filtered = bookings.Values
                    .Where(b => b.OwnerId == oId && (!status.HasValue || b.Status == status.Value))
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList();
                var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                return ((IReadOnlyList<Booking>)paged, filtered.Count);
            });

        var usersMock = new Mock<IUsersApi>();
        usersMock.Setup(x => x.GetBookingEligibilityAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new BookingEligibilityDto(true, null)));
        usersMock.Setup(x => x.GetContactAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, CancellationToken _) => Result.Success(
                new UserContactDto(userId, "customer@example.com", "0901234567", "Nguyen Van A")));

        var fleetMock = new Mock<IFleetApi>();
        fleetMock.Setup(x => x.GetVehicleSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, CancellationToken _) => Result.Success(
                new VehicleSummaryDto(
                    vehicleId,
                    ownerId,
                    "Published",
                    Guid.NewGuid(),
                    "Toyota",
                    Guid.NewGuid(),
                    "Camry",
                    "51A-12345",
                    "Auto",
                    5,
                    null,
                    GeoPoint.Create(10.7769, 106.7009).Value,
                    "Ho Chi Minh City")));

        var pricingMock = new Mock<IPricingApi>();
        pricingMock.Setup(x => x.GetQuoteAsync(It.IsAny<Guid>(), It.IsAny<DateTimeRange>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid vehicleId, DateTimeRange _, CancellationToken _) => Result.Success(
                new QuoteDto(
                    vehicleId,
                    Money.Of(2_000_000),
                    Money.Of(5_000_000),
                    300,
                    null,
                    [
                        new QuoteLineDto("BaseRate", "Rent", Money.Of(2_000_000)),
                        new QuoteLineDto("Deposit", "Deposit", Money.Of(5_000_000))
                    ],
                    new CancellationPolicyDto([
                        new CancellationTierDto(48, 100m),
                        new CancellationTierDto(24, 70m),
                        new CancellationTierDto(0, 0m)
                    ]))));

        var availabilityMock = new Mock<IAvailabilityApi>();
        availabilityMock.Setup(x => x.PlaceHoldAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeRange>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        availabilityMock.Setup(x => x.ConfirmHoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        availabilityMock.Setup(x => x.ReleaseHoldAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var outboxMock = new Mock<IEventOutbox>();
        outboxMock.Setup(x => x.PublishAsync(It.IsAny<IIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new BookingApiService(
            dbContextMock.Object,
            repoMock.Object,
            usersMock.Object,
            fleetMock.Object,
            pricingMock.Object,
            availabilityMock.Object,
            outboxMock.Object,
            clockMock.Object);

        return (service, ownerId, bookings, dbContextMock, usersMock, fleetMock, pricingMock, availabilityMock, outboxMock);
    }
}
