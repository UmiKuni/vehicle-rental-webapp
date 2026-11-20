using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Xunit;

namespace CarRental.IntegrationTests.Payments;

public class PaymentsIntegrationTests : IntegrationTestBase
{
    private const string VnPayTmnCode = "TESTTMN";
    private const string VnPayHashSecret = "vnpay-test-hash-secret-value-123456";
    private const string StripeWebhookSecret = "whsec_test_secret_123456";
    private const string TransferWebhookSecret = "transfer-test-webhook-secret-123";

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
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Payments:UseMockGateway"] = "false",
                    ["Payments:VNPay:TmnCode"] = VnPayTmnCode,
                    ["Payments:VNPay:HashSecret"] = VnPayHashSecret,
                    ["Payments:VNPay:BaseUrl"] = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
                    ["Payments:Stripe:SecretKey"] = "sk_test_dummy_key_for_integration",
                    ["Payments:Stripe:WebhookSecret"] = StripeWebhookSecret,
                    ["Payments:Stripe:Currency"] = "vnd",
                    ["Payments:Transfer:BankCode"] = "VCB",
                    ["Payments:Transfer:AccountNumber"] = "0123456789",
                    ["Payments:Transfer:AccountName"] = "CAR RENTAL TEST",
                    ["Payments:Transfer:WebhookSecret"] = TransferWebhookSecret
                });
            });

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

        // 1. Initiate real VNPay checkout
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
        checkout.PaymentUrl.Should().Contain($"vnp_TmnCode={VnPayTmnCode}");
        checkout.PaymentUrl.Should().Contain("vnp_Amount=670000000");
        checkout.PaymentUrl.Should().Contain($"vnp_TxnRef={checkout.PaymentId:N}");
        checkout.PaymentUrl.Should().Contain("vnp_SecureHash=");

        // 2. Invalid webhook signatures are rejected
        var badSigResponse = await anonymousClient.PostAsJsonAsync(
            "/api/payments/webhook",
            new PaymentWebhookRequest(
                "VNP-TXN-BAD",
                checkout.PaymentId.ToString(),
                6_700_000m,
                "00",
                "INVALID"));
        badSigResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var badIpnResponse = await anonymousClient.GetAsync(
            $"/api/payments/webhooks/vnpay?vnp_TmnCode={VnPayTmnCode}&vnp_Amount=670000000&vnp_TxnRef={checkout.PaymentId:N}&vnp_ResponseCode=00&vnp_TransactionStatus=00&vnp_TransactionNo=VNP-TXN-001&vnp_SecureHash=badhash");
        badIpnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var badIpnDto = await badIpnResponse.Content.ReadFromJsonAsync<VnPayIpnResponseDto>();
        badIpnDto!.RspCode.Should().Be("97");

        // 3. Valid HMAC-SHA512 signed VNPay IPN succeeds, marks booking paid, holds deposit, writes inbox
        var ipnQuery = BuildSignedVnPayIpnQuery(
            checkout.PaymentId,
            6_700_000m,
            "VNP-TXN-001",
            "00");
        var webhookResponse = await anonymousClient.GetAsync($"/api/payments/webhooks/vnpay?{ipnQuery}");

        webhookResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var webhookDto = await webhookResponse.Content.ReadFromJsonAsync<VnPayIpnResponseDto>();
        webhookDto!.RspCode.Should().Be("00");
        _stubBookingApi.MarkPaidCallCount(bookingId).Should().Be(1);

        // 4. Duplicate VNPay IPN with same TransactionId is idempotent (RspCode 02)
        var duplicateResponse = await anonymousClient.GetAsync($"/api/payments/webhooks/vnpay?{ipnQuery}");

        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicateDto = await duplicateResponse.Content.ReadFromJsonAsync<VnPayIpnResponseDto>();
        duplicateDto!.RspCode.Should().Be("02");
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
    public async Task BankTransferCheckout_And_Webhook_ShouldPersistAndEnforceSignature()
    {
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_trf@example.com",
            "0901110005");
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_trf@example.com",
            "0901110006",
            taxCode: "0102030903");

        var customerClient = CreatePaymentsClient(customerAuthClient);
        var anonymousClient = _paymentsFactory!.CreateClient();

        var bookingId = Guid.NewGuid();
        _stubBookingApi.SeedBooking(new BookingRentalDetailsDto(
            bookingId,
            customerId,
            ownerId,
            Guid.NewGuid(),
            "Confirmed",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(3),
            1_200_000m,
            3_000_000m));

        var checkoutResponse = await customerClient.PostAsJsonAsync(
            "/api/payments/checkout",
            new InitiatePaymentRequest(bookingId, "Transfer", "https://localhost:5173/payment/result"));

        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var checkout = await checkoutResponse.Content.ReadFromJsonAsync<PaymentCheckoutDto>(JsonOptions);
        checkout!.PaymentUrl.Should().Contain("https://img.vietqr.io/image/VCB-0123456789-compact2.png");
        checkout.PaymentUrl.Should().Contain("amount=4200000");
        checkout.PaymentUrl.Should().Contain($"addInfo=CARRENTAL%20{checkout.PaymentId:N}");

        var payloadJson = $$"""{"id":55001,"referenceCode":"FT263230099","transferType":"in","transferAmount":4200000,"content":"CARRENTAL {{checkout.PaymentId:N}}"}""";

        // Wrong API key rejected with 401
        using var badRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/transfer");
        badRequest.Headers.Authorization = new AuthenticationHeaderValue("Apikey", "wrong-secret");
        badRequest.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        var badResponse = await anonymousClient.SendAsync(badRequest);
        badResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Valid API key accepted
        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/transfer");
        validRequest.Headers.Authorization = new AuthenticationHeaderValue("Apikey", TransferWebhookSecret);
        validRequest.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        var okResponse = await anonymousClient.SendAsync(validRequest);
        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _stubBookingApi.MarkPaidCallCount(bookingId).Should().Be(1);
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var payment = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == PaymentId.From(checkout.PaymentId));
        payment.Status.Should().Be(PaymentStatus.Success);
        payment.GatewayTransactionId.Should().Be("FT263230099");
    }

    [Fact]
    public async Task StripeWebhook_ShouldVerifyHmacSha256AndMarkPaymentPaid()
    {
        var (customerAuthClient, customerId) = await CreateAuthenticatedCustomerClientAsync(
            "cust_stripe@example.com",
            "0901110007");
        var (_, ownerId) = await CreateAuthenticatedOwnerClientAsync(
            "owner_stripe@example.com",
            "0901110008",
            taxCode: "0102030904");
        _ = customerAuthClient;

        var anonymousClient = _paymentsFactory!.CreateClient();
        var bookingId = Guid.NewGuid();
        _stubBookingApi.SeedBooking(new BookingRentalDetailsDto(
            bookingId,
            customerId,
            ownerId,
            Guid.NewGuid(),
            "Confirmed",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            1_000_000m,
            2_000_000m));

        var pendingPayment = Payment.CreatePending(bookingId, Money.Of(3_000_000), Money.Of(2_000_000), DateTimeOffset.UtcNow).Value;
        pendingPayment.SetPaymentMethod(PaymentMethod.Stripe);

        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            db.Payments.Add(pendingPayment);
            await db.SaveChangesAsync();
        }

        var stripePayload = $$"""
            {
              "id": "evt_test_001",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "id": "cs_test_001",
                  "payment_intent": "pi_test_123456",
                  "payment_status": "paid",
                  "amount_total": 3000000,
                  "currency": "vnd",
                  "metadata": {
                    "payment_id": "{{pendingPayment.Id.Value}}"
                  }
                }
              }
            }
            """;

        // Invalid signature rejected with 401
        using var badRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe");
        badRequest.Headers.TryAddWithoutValidation("Stripe-Signature", $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()},v1=invalid");
        badRequest.Content = new StringContent(stripePayload, Encoding.UTF8, "application/json");
        var badResponse = await anonymousClient.SendAsync(badRequest);
        badResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Valid Stripe-Signature accepted
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{stripePayload}";
        var sig = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(StripeWebhookSecret), Encoding.UTF8.GetBytes(signedPayload)));

        using var validRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/webhooks/stripe");
        validRequest.Headers.TryAddWithoutValidation("Stripe-Signature", $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={sig}");
        validRequest.Content = new StringContent(stripePayload, Encoding.UTF8, "application/json");
        var okResponse = await anonymousClient.SendAsync(validRequest);
        okResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        _stubBookingApi.MarkPaidCallCount(bookingId).Should().Be(1);
        using (var scope = CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var saved = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == pendingPayment.Id);
            saved.Status.Should().Be(PaymentStatus.Success);
            saved.GatewayTransactionId.Should().Be("pi_test_123456");

            var deposit = await db.Deposits.AsNoTracking().SingleAsync(d => d.BookingId == bookingId);
            deposit.Status.Should().Be(DepositStatus.Held);
        }
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

    [Fact]
    public async Task EventDriven_BookingExpired_And_PaidBookingCancelled_ShouldCancelPendingAndRefundPaid()
    {
        var expiredBookingId = Guid.NewGuid();
        var paidCancelledBookingId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var expiredPendingPayment = Payment.CreatePending(
            expiredBookingId, Money.Of(3_000_000), Money.Of(2_000_000), now).Value;

        var paidPayment = Payment.CreatePending(
            paidCancelledBookingId, Money.Of(4_000_000), Money.Of(2_000_000), now).Value;
        paidPayment.SetPaymentMethod(PaymentMethod.Transfer);
        paidPayment.MarkSuccess("TRF-PAID-001", now);
        var heldDeposit = Deposit.Hold(paidCancelledBookingId, Money.Of(2_000_000), now).Value;

        using (var scope = _paymentsFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            db.Payments.AddRange(expiredPendingPayment, paidPayment);
            db.Deposits.Add(heldDeposit);
            await db.SaveChangesAsync();

            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.PublishAsync(new BookingExpired(
                Guid.NewGuid(),
                now,
                expiredBookingId,
                "Payment deadline expired"));

            await bus.PublishAsync(new BookingCancelled(
                Guid.NewGuid(),
                now,
                paidCancelledBookingId,
                Guid.NewGuid(),
                Money.Zero(),
                Money.Of(4_000_000),
                "Admin full refund"));
        }

        var updated = false;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var scope = _paymentsFactory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var expiredSaved = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == expiredPendingPayment.Id);
            var refundedSaved = await db.Payments.AsNoTracking().SingleAsync(p => p.Id == paidPayment.Id);
            var depositSaved = await db.Deposits.AsNoTracking().SingleAsync(d => d.Id == heldDeposit.Id);

            if (expiredSaved.Status == PaymentStatus.Failed
                && refundedSaved.Status == PaymentStatus.Refunded
                && refundedSaved.RefundedAmount.Amount == 4_000_000
                && depositSaved.Status == DepositStatus.Released)
            {
                updated = true;
                break;
            }

            await Task.Delay(150);
        }

        updated.Should().BeTrue();
    }

    private static string BuildSignedVnPayIpnQuery(
        Guid paymentId,
        decimal amountVnd,
        string transactionNo,
        string responseCode)
    {
        var amountMinor = ((long)Math.Round(amountVnd, MidpointRounding.AwayFromZero) * 100)
            .ToString(CultureInfo.InvariantCulture);

        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amountMinor,
            ["vnp_CurrCode"] = "VND",
            ["vnp_PayDate"] = "20261119170500",
            ["vnp_ResponseCode"] = responseCode,
            ["vnp_TmnCode"] = VnPayTmnCode,
            ["vnp_TransactionNo"] = transactionNo,
            ["vnp_TransactionStatus"] = responseCode,
            ["vnp_TxnRef"] = paymentId.ToString("N")
        };

        var signData = string.Join('&', sorted.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));
        var hash = Convert.ToHexStringLower(
            HMACSHA512.HashData(Encoding.UTF8.GetBytes(VnPayHashSecret), Encoding.UTF8.GetBytes(signData)));
        return $"{signData}&vnp_SecureHash={hash}";
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
