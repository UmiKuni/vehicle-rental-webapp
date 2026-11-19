using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.IntegrationTests.Fixtures;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Payments;

public class PaymentsIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly StubBookingApi _stubBookingApi = new();
    private WebApplicationFactory<Program>? _paymentsFactory;

    public PaymentsIntegrationTests(CarRentalApiFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _stubBookingApi.Reset();
        _paymentsFactory ??= Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBookingApi>();
                services.AddSingleton<IBookingApi>(_stubBookingApi);
            });
        });
    }

    public override async Task DisposeAsync()
    {
        if (_paymentsFactory is not null)
        {
            await _paymentsFactory.DisposeAsync();
            _paymentsFactory = null;
        }

        await base.DisposeAsync();
    }

    private HttpClient CreatePaymentsClient(HttpClient authenticatedSourceClient)
    {
        var client = _paymentsFactory!.CreateClient();
        client.DefaultRequestHeaders.Authorization = authenticatedSourceClient.DefaultRequestHeaders.Authorization;
        return client;
    }

    [Fact]
    public async Task Checkout_WebhookIpn_And_DuplicateWebhook_ShouldPersistAndBeIdempotent()
    {
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_pay@example.com",
            "0901110001");
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_pay@example.com",
            "0901110002",
            taxCode: "0102030901");

        var customerClient = CreatePaymentsClient(customerAuthClient);
        var anonymousClient = _paymentsFactory!.CreateClient();

        var bookingId = Guid.NewGuid();
        _stubBookingApi.SeedBooking(new BookingRentalDetailsDto(
            bookingId,
            customerId,
            ownerId,
            Guid.NewGuid(),
            "Confirmed",
            DateTimeOffset.UtcNow.AddDays(2),
            DateTimeOffset.UtcNow.AddDays(4),
            1_700_000m,
            5_000_000m));

        // 1. Initiate checkout
        var checkoutResponse = await customerClient.PostAsJsonAsync(
            "/api/payments/checkout",
            new InitiatePaymentRequest(bookingId, "VNPay", "https://localhost:5173/payment/result"));

        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<PaymentCheckoutDto>(JsonOptions);
        checkout.Should().NotBeNull();
        checkout!.BookingId.Should().Be(bookingId);
        checkout.Amount.Should().Be(6_700_000m);
        checkout.Method.Should().Be(PaymentMethod.VNPay);
        checkout.PaymentUrl.Should().Contain("vnpayment.vn");

        // 2. Invalid webhook signature returns 401 Unauthorized
        var badSigResponse = await anonymousClient.PostAsJsonAsync(
            "/api/payments/webhook",
            new PaymentWebhookRequest(
                "VNP-TXN-BAD",
                checkout.PaymentId.ToString(),
                6_700_000m,
                "00",
                "INVALID"));
        badSigResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 3. Valid webhook IPN succeeds, marks booking paid, holds deposit, writes inbox
        var webhookResponse = await anonymousClient.PostAsJsonAsync(
            "/api/payments/webhook",
            new PaymentWebhookRequest(
                "VNP-TXN-001",
                checkout.PaymentId.ToString(),
                6_700_000m,
                "00",
                "valid-mock-signature"));

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var webhookDto = await webhookResponse.Content.ReadFromJsonAsync<PaymentWebhookResponseDto>();
        webhookDto!.Success.Should().BeTrue();
        _stubBookingApi.MarkPaidCallCount(bookingId).Should().Be(1);

        // 4. Duplicate webhook with same TransactionId is idempotent
        var duplicateResponse = await anonymousClient.PostAsJsonAsync(
            "/api/payments/webhook",
            new PaymentWebhookRequest(
                "VNP-TXN-001",
                checkout.PaymentId.ToString(),
                6_700_000m,
                "00",
                "valid-mock-signature"));

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicateDto = await duplicateResponse.Content.ReadFromJsonAsync<PaymentWebhookResponseDto>();
        duplicateDto!.Success.Should().BeTrue();
        duplicateDto.Message.Should().Be("Webhook already processed.");
        _stubBookingApi.MarkPaidCallCount(bookingId).Should().Be(1);

        // 5. Verify persisted PostgreSQL state
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == PaymentId.From(checkout.PaymentId));
            payment.Status.Should().Be(PaymentStatus.Success);
            payment.GatewayTransactionId.Should().Be("VNP-TXN-001");

            var deposit = await db.Deposits.AsNoTracking().SingleAsync(d => d.BookingId == bookingId);
            deposit.Status.Should().Be(DepositStatus.Held);
            deposit.Amount.Amount.Should().Be(5_000_000);

            var inboxExists = await db.WebhookInboxes.AsNoTracking().AnyAsync(w => w.Id == "VNP-TXN-001");
            inboxExists.Should().BeTrue();
        }

        // 6. Participant can query payment by ID and by booking ID
        var getByIdResponse = await customerClient.GetAsync($"/api/payments/{checkout.PaymentId}");
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var paymentDetail = await getByIdResponse.Content.ReadFromJsonAsync<PaymentDetailDto>();
        paymentDetail!.Status.Should().Be("Success");
        paymentDetail.GatewayTransactionId.Should().Be("VNP-TXN-001");

        var summaryResponse = await customerClient.GetAsync($"/api/payments/booking/{bookingId}");
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await summaryResponse.Content.ReadFromJsonAsync<BookingPaymentSummaryDto>();
        summary!.MainPayment.Should().NotBeNull();
        summary.Deposit.Should().NotBeNull();
        summary.Deposit!.Status.Should().Be("Held");
    }

    [Fact]
    public async Task DepositRelease_And_Deduct_ShouldEnforceOwnerAndPersist()
    {
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_dep@example.com",
            "0901110003");
        var (ownerAuthClient, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_dep@example.com",
            "0901110004",
            taxCode: "0102030902");

        var customerClient = CreatePaymentsClient(customerAuthClient);
        var ownerClient = CreatePaymentsClient(ownerAuthClient);

        var bookingReleaseId = Guid.NewGuid();
        var bookingDeductId = Guid.NewGuid();
        _stubBookingApi.SeedBooking(new BookingRentalDetailsDto(
            bookingReleaseId, customerId, ownerId, Guid.NewGuid(), "Completed",
            DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1), 2_000_000m, 5_000_000m));
        _stubBookingApi.SeedBooking(new BookingRentalDetailsDto(
            bookingDeductId, customerId, ownerId, Guid.NewGuid(), "Completed",
            DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow.AddDays(-1), 2_000_000m, 5_000_000m));

        var depositRelease = Deposit.Hold(bookingReleaseId, Money.Of(5_000_000), DateTimeOffset.UtcNow).Value;
        var depositDeduct = Deposit.Hold(bookingDeductId, Money.Of(5_000_000), DateTimeOffset.UtcNow).Value;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            db.Deposits.AddRange(depositRelease, depositDeduct);
            await db.SaveChangesAsync();
        }

        // Customer cannot release or deduct (403 Forbidden)
        var forbiddenRelease = await customerClient.PostAsJsonAsync(
            $"/api/payments/deposits/{depositRelease.Id.Value}/release",
            new ReleaseDepositRequest("Trying to self-release"));
        forbiddenRelease.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Owner releases full deposit
        var releaseResponse = await ownerClient.PostAsJsonAsync(
            $"/api/payments/deposits/{depositRelease.Id.Value}/release",
            new ReleaseDepositRequest("All good"));
        releaseResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var releasedDto = await releaseResponse.Content.ReadFromJsonAsync<DepositActionDto>();
        releasedDto!.Status.Should().Be("Released");
        releasedDto.RefundedAmount.Should().Be(5_000_000m);

        // Owner deducts partial amount
        var deductResponse = await ownerClient.PostAsJsonAsync(
            $"/api/payments/deposits/{depositDeduct.Id.Value}/deduct",
            new DeductDepositRequest(750_000m, "Minor scratch"));
        deductResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deductedDto = await deductResponse.Content.ReadFromJsonAsync<DepositActionDto>();
        deductedDto!.Status.Should().Be("Deducted");
        deductedDto.DeductedAmount.Should().Be(750_000m);
        deductedDto.RefundedAmount.Should().Be(4_250_000m);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var savedRelease = await db.Deposits.AsNoTracking().SingleAsync(d => d.Id == depositRelease.Id);
            savedRelease.Status.Should().Be(DepositStatus.Released);

            var savedDeduct = await db.Deposits.AsNoTracking().SingleAsync(d => d.Id == depositDeduct.Id);
            savedDeduct.Status.Should().Be(DepositStatus.Deducted);
            savedDeduct.DeductedAmount.Amount.Should().Be(750_000);
        }
    }

    [Fact]
    public async Task EventDriven_BookingCancelled_And_HandoverReturn_ShouldUpdatePaymentsDb()
    {
        var cancelledBookingId = Guid.NewGuid();
        var returnedBookingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var pendingPayment = Payment.CreatePending(cancelledBookingId, Money.Of(3_000_000), Money.Of(2_000_000), now).Value;
        var returnDeposit = Deposit.Hold(returnedBookingId, Money.Of(4_000_000), now).Value;

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            db.Payments.Add(pendingPayment);
            db.Deposits.Add(returnDeposit);
            await db.SaveChangesAsync();

            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new BookingCancelled(
                Guid.NewGuid(),
                now,
                cancelledBookingId,
                Guid.NewGuid(),
                Money.Zero(),
                Money.Zero(),
                "Cancelled before payment"));

            await bus.PublishAsync(new HandoverConfirmed(
                Guid.NewGuid(),
                now,
                returnedBookingId,
                "Return",
                12500,
                90,
                now,
                120));
        }

        var updated = false;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var p = await db.Payments.AsNoTracking().SingleAsync(x => x.Id == pendingPayment.Id);
            var d = await db.Deposits.AsNoTracking().SingleAsync(x => x.Id == returnDeposit.Id);
            if (p.Status == PaymentStatus.Failed && d.ReleaseAfter is not null)
            {
                updated = true;
                break;
            }

            await Task.Delay(150);
        }

        updated.Should().BeTrue();
    }

    private sealed class StubBookingApi : IBookingApi
    {
        private readonly ConcurrentDictionary<Guid, BookingRentalDetailsDto> _bookings = new();
        private readonly ConcurrentDictionary<Guid, int> _markPaidCounts = new();

        public void Reset()
        {
            _bookings.Clear();
            _markPaidCounts.Clear();
        }

        public void SeedBooking(BookingRentalDetailsDto details) =>
            _bookings[details.BookingId] = details;

        public int MarkPaidCallCount(Guid bookingId) =>
            _markPaidCounts.GetValueOrDefault(bookingId, 0);

        public Task<bool> IsParticipantAsync(Guid bookingId, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_bookings.TryGetValue(bookingId, out var b)
                            && (b.CustomerId == userId || b.OwnerId == userId));

        public Task<Result> MarkPaidAsync(Guid bookingId, Guid paymentId, DateTimeOffset paidAt, CancellationToken ct = default)
        {
            _markPaidCounts.AddOrUpdate(bookingId, 1, (_, count) => count + 1);
            if (_bookings.TryGetValue(bookingId, out var existing))
            {
                _bookings[bookingId] = existing with { Status = "Paid" };
            }

            return Task.FromResult(Result.Success());
        }

        public Task<Result<BookingRentalDetailsDto>> GetRentalDetailsAsync(Guid bookingId, CancellationToken ct = default) =>
            _bookings.TryGetValue(bookingId, out var details)
                ? Task.FromResult(Result.Success(details))
                : Task.FromResult(Result.Failure<BookingRentalDetailsDto>(Error.NotFound("booking.not_found", "Booking not found.")));

        public Task<Result<BookingDetailDto>> CreateAsync(Guid customerId, CreateBookingRequest request, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<PagedResult<BookingSummaryDto>>> GetMyBookingsAsync(Guid customerId, string? status = null, int page = 1, int pageSize = 10, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<PagedResult<BookingSummaryDto>>> GetOwnerBookingsAsync(Guid ownerId, string? status = null, int page = 1, int pageSize = 10, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingDetailDto>> GetByIdAsync(Guid bookingId, Guid actorId, bool isAdmin = false, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingDetailDto>> ApproveAsync(Guid bookingId, Guid ownerId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingDetailDto>> RejectAsync(Guid bookingId, Guid ownerId, string reason, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingCancellationResultDto>> CancelAsync(Guid bookingId, Guid customerId, string reason, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result<BookingDetailDto>> AdminCancelAsync(Guid bookingId, Guid adminId, string reason, string penaltyParty, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result> StartTripAsync(Guid bookingId, DateTimeOffset startedAt, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<Result> CompleteTripAsync(Guid bookingId, decimal? overKmFee, DateTimeOffset completedAt, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
