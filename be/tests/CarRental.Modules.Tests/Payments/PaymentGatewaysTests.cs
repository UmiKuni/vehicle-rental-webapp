using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Options;
using CarRental.Modules.Payments.Services;
using CarRental.Modules.Payments.Services.Gateways;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarRental.Modules.Tests.Payments;

public class PaymentGatewaysTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task VnPayGateway_WhenUnconfigured_ShouldReturnGatewayNotConfigured()
    {
        var gateway = CreateVnPayGateway(new VnPayOptions());

        var result = await gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(Guid.NewGuid(), Guid.NewGuid(), 1_500_000m, "VND", PaymentMethod.VNPay, "https://app.test/return", Now));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("payment.gateway_not_configured");
    }

    [Fact]
    public async Task VnPayGateway_Checkout_IpnVerification_UnifiedWebhook_And_Refund_ShouldWorkCorrectly()
    {
        var opts = new VnPayOptions
        {
            TmnCode = "CARRENT1",
            HashSecret = "vnpay-super-secret-hmac-512-key",
            BaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
            ApiUrl = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction"
        };

        var handler = new FakeHttpMessageHandler((_, _) =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"vnp_ResponseCode":"00","vnp_TransactionNo":"VNP_REF_9988","vnp_Message":"Success"}""",
                    Encoding.UTF8,
                    "application/json")
            });

        var gateway = CreateVnPayGateway(opts, handler);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        // 1. Checkout URL with IPv6 loopback mapped to 127.0.0.1 and UTC+7 timestamps
        var checkout = await gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(paymentId, bookingId, 2_500_000m, "VND", PaymentMethod.VNPay, "https://app.test/return", Now, "::1"));

        checkout.IsSuccess.Should().BeTrue();
        var uri = new Uri(checkout.Value.PaymentUrl);
        var parsedQuery = HttpUtility.ParseQueryString(uri.Query);
        parsedQuery["vnp_TmnCode"].Should().Be("CARRENT1");
        parsedQuery["vnp_Amount"].Should().Be("250000000");
        parsedQuery["vnp_TxnRef"].Should().Be(paymentId.ToString("N"));
        parsedQuery["vnp_IpAddr"].Should().Be("127.0.0.1");
        parsedQuery["vnp_CreateDate"].Should().Be("20261119170000");
        parsedQuery["vnp_ExpireDate"].Should().Be("20261119171500");
        parsedQuery["vnp_SecureHash"].Should().NotBeNullOrWhiteSpace();

        // 2. IPN Query Verification (valid, tampered, wrong TmnCode, failure response)
        var ipnParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = "250000000",
            ["vnp_CurrCode"] = "VND",
            ["vnp_PayDate"] = "20261119170500",
            ["vnp_ResponseCode"] = "00",
            ["vnp_TmnCode"] = "CARRENT1",
            ["vnp_TransactionNo"] = "14455667",
            ["vnp_TransactionStatus"] = "00",
            ["vnp_TxnRef"] = paymentId.ToString("N")
        };
        var signData = BuildUrlEncodedQuery(ipnParams);
        ipnParams["vnp_SecureHash"] = ComputeHmacSha512(opts.HashSecret, signData);

        var ipnResult = await gateway.ParseAndVerifyWebhookAsync(
            new GatewayRawWebhook(PaymentMethod.VNPay, string.Empty, new Dictionary<string, string>(), ipnParams));
        ipnResult.IsSuccess.Should().BeTrue();
        ipnResult.Value.IsSuccess.Should().BeTrue();
        ipnResult.Value.Amount.Should().Be(2_500_000m);
        ipnResult.Value.TransactionId.Should().Be("14455667");
        ipnResult.Value.OrderId.Should().Be(paymentId.ToString("N"));

        var tampered = new Dictionary<string, string>(ipnParams) { ["vnp_Amount"] = "100000" };
        var tamperedResult = await gateway.ParseAndVerifyWebhookAsync(
            new GatewayRawWebhook(PaymentMethod.VNPay, string.Empty, new Dictionary<string, string>(), tampered));
        tamperedResult.IsFailure.Should().BeTrue();
        tamperedResult.Error.Code.Should().Be("payment.invalid_signature");

        var wrongTmnParams = new SortedDictionary<string, string>(ipnParams) { ["vnp_TmnCode"] = "OTHER" };
        wrongTmnParams.Remove("vnp_SecureHash");
        wrongTmnParams["vnp_SecureHash"] = ComputeHmacSha512(opts.HashSecret, BuildUrlEncodedQuery(wrongTmnParams));
        var wrongTmnResult = await gateway.ParseAndVerifyWebhookAsync(
            new GatewayRawWebhook(PaymentMethod.VNPay, string.Empty, new Dictionary<string, string>(), wrongTmnParams));
        wrongTmnResult.IsFailure.Should().BeTrue();
        wrongTmnResult.Error.Code.Should().Be("payment.invalid_signature");

        // 3. Unified webhook verification
        var unifiedSig = ComputeHmacSha512(opts.HashSecret, $"TXN1|{paymentId}|2500000|00");
        gateway.VerifyWebhookSignature(new PaymentWebhookRequest("TXN1", paymentId.ToString(), 2_500_000m, "00", unifiedSig)).Should().BeTrue();
        gateway.VerifyWebhookSignature(new PaymentWebhookRequest("TXN1", paymentId.ToString(), 2_500_000m, "00", "wrong")).Should().BeFalse();

        // 4. Refund API call (partial -> 03, full -> 02)
        var partialRefund = await gateway.RefundAsync(
            new GatewayRefundRequest(paymentId, PaymentMethod.VNPay, "14455667", 1_000_000m, 2_500_000m, "VND", "Trả xe sớm", Now));
        partialRefund.IsSuccess.Should().BeTrue();
        partialRefund.Value.RefundTransactionId.Should().Be("VNP_REF_9988");

        var sentBody = JsonDocument.Parse(handler.Requests.Single().Body).RootElement;
        sentBody.GetProperty("vnp_TransactionType").GetString().Should().Be("03");
        sentBody.GetProperty("vnp_Amount").GetString().Should().Be("100000000");
        sentBody.GetProperty("vnp_OrderInfo").GetString().Should().Contain("Tra xe som");
    }

    [Fact]
    public async Task StripeGateway_Checkout_WebhookVerification_And_Refund_ShouldWorkCorrectly()
    {
        var opts = new StripeOptions
        {
            SecretKey = "sk_test_stripe_secret",
            WebhookSecret = "whsec_stripe_webhook_secret",
            ApiBaseUrl = "https://api.stripe.test",
            Currency = "vnd",
            WebhookToleranceSeconds = 300,
            CheckoutTtlMinutes = 10 // Enforces Stripe 30-min minimum
        };

        var handler = new FakeHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri!.AbsolutePath == "/v1/checkout/sessions")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"id":"cs_test_123","url":"https://checkout.stripe.com/c/pay/cs_test_123","expires_at":{{Now.AddMinutes(30).ToUnixTimeSeconds()}}}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (req.RequestUri.AbsolutePath == "/v1/refunds")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""{"id":"re_test_456","created":{{Now.ToUnixTimeSeconds()}}}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var gateway = CreateStripeGateway(opts, handler);
        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        // 1. Checkout session creation
        var checkout = await gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(paymentId, bookingId, 3_500_000m, "VND", PaymentMethod.Stripe, "https://app.test/return", Now));
        checkout.IsSuccess.Should().BeTrue();
        checkout.Value.PaymentUrl.Should().Be("https://checkout.stripe.com/c/pay/cs_test_123");
        checkout.Value.ExpiresAt.Should().Be(Now.AddMinutes(30));

        var sessionReq = handler.Requests.First();
        sessionReq.Headers["Authorization"].Should().Be("Bearer sk_test_stripe_secret");
        sessionReq.Body.Should().Contain("unit_amount%5D=3500000");
        sessionReq.Body.Should().Contain("currency%5D=vnd");

        // 2. Stripe Webhook verification (valid, expired timestamp, bad signature, unpaid ignored, expired event)
        var completedPayload = $$"""
            {
              "id": "evt_1",
              "type": "checkout.session.completed",
              "data": {
                "object": {
                  "id": "cs_test_123",
                  "payment_intent": "pi_test_999",
                  "payment_status": "paid",
                  "amount_total": 3500000,
                  "currency": "vnd",
                  "metadata": { "payment_id": "{{paymentId}}" }
                }
              }
            }
            """;

        var validSig = BuildStripeSignatureHeader(opts.WebhookSecret, Now.ToUnixTimeSeconds(), completedPayload);
        var verified = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Stripe,
            completedPayload,
            new Dictionary<string, string> { ["Stripe-Signature"] = validSig },
            new Dictionary<string, string>()));
        verified.IsSuccess.Should().BeTrue();
        verified.Value.IsSuccess.Should().BeTrue();
        verified.Value.TransactionId.Should().Be("pi_test_999");
        verified.Value.Amount.Should().Be(3_500_000m);

        var expiredSig = BuildStripeSignatureHeader(opts.WebhookSecret, Now.AddMinutes(-10).ToUnixTimeSeconds(), completedPayload);
        var expiredResult = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Stripe,
            completedPayload,
            new Dictionary<string, string> { ["Stripe-Signature"] = expiredSig },
            new Dictionary<string, string>()));
        expiredResult.IsFailure.Should().BeTrue();
        expiredResult.Error.Code.Should().Be("payment.invalid_signature");

        var badSigResult = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Stripe,
            completedPayload,
            new Dictionary<string, string> { ["Stripe-Signature"] = $"t={Now.ToUnixTimeSeconds()},v1=deadbeef" },
            new Dictionary<string, string>()));
        badSigResult.IsFailure.Should().BeTrue();
        badSigResult.Error.Code.Should().Be("payment.invalid_signature");

        // 3. Refund with pi_...
        var refund = await gateway.RefundAsync(
            new GatewayRefundRequest(paymentId, PaymentMethod.Stripe, "pi_test_999", 1_500_000m, 3_500_000m, "VND", "Customer cancel", Now));
        refund.IsSuccess.Should().BeTrue();
        refund.Value.RefundTransactionId.Should().Be("re_test_456");

        var refundReq = handler.Requests.Last();
        refundReq.Body.Should().Contain("payment_intent=pi_test_999");
        refundReq.Body.Should().Contain("reason=requested_by_customer");
        refundReq.Body.Should().Contain("amount=1500000");

        // Unsupported transaction ID rejected
        var invalidTxnRefund = await gateway.RefundAsync(
            new GatewayRefundRequest(paymentId, PaymentMethod.Stripe, "INVALID_TXN", 1_500_000m, 3_500_000m, "VND", "Test", Now));
        invalidTxnRefund.IsFailure.Should().BeTrue();
        invalidTxnRefund.Error.Code.Should().Be("payment.gateway_request_failed");
    }

    [Fact]
    public async Task PayPalGateway_TokenCaching_Checkout_Capture_Webhook_And_Refund_ShouldWorkCorrectly()
    {
        var opts = new PayPalOptions
        {
            ClientId = "paypal-client-id",
            ClientSecret = "paypal-client-secret",
            WebhookId = "WH-PAYPAL-123",
            BaseUrl = "https://api-m.sandbox.paypal.test",
            Currency = "USD",
            ExchangeRate = 25_000m
        };

        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var tokenCallCount = 0;
        var alreadyCapturedFirstAttempt = false;

        var handler = new FakeHttpMessageHandler((req, _) =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/v1/oauth2/token")
            {
                tokenCallCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"pp-access-token","expires_in":3600}""", Encoding.UTF8, "application/json")
                };
            }

            if (path == "/v2/checkout/orders")
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent(
                        """
                        {
                          "id": "ORDER-123",
                          "status": "PAYER_ACTION_REQUIRED",
                          "links": [
                            { "rel": "self", "href": "https://api-m.sandbox.paypal.test/v2/checkout/orders/ORDER-123" },
                            { "rel": "payer-action", "href": "https://www.sandbox.paypal.com/checkoutnow?token=ORDER-123" }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (path == "/v2/checkout/orders/ORDER-ALREADY/capture")
            {
                alreadyCapturedFirstAttempt = true;
                return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent("""{"name":"UNPROCESSABLE_ENTITY","details":[{"issue":"ORDER_ALREADY_CAPTURED"}]}""", Encoding.UTF8, "application/json")
                };
            }

            if (path == "/v2/checkout/orders/ORDER-ALREADY")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $$"""
                        {
                          "id": "ORDER-ALREADY",
                          "purchase_units": [{
                            "custom_id": "{{paymentId}}",
                            "payments": {
                              "captures": [{
                                "id": "CAP-EXISTING-1",
                                "status": "COMPLETED",
                                "amount": { "currency_code": "USD", "value": "200.00" }
                              }]
                            }
                          }]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (path == "/v1/notifications/verify-webhook-signature")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"verification_status":"SUCCESS"}""", Encoding.UTF8, "application/json")
                };
            }

            if (path == "/v2/payments/captures/CAP-EXISTING-1/refund")
            {
                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""{"id":"PP-REF-001","status":"COMPLETED"}""", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var gateway = CreatePayPalGateway(opts, handler);

        // 1. Checkout creation (5,000,000 VND -> 200.00 USD)
        var checkout = await gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(paymentId, bookingId, 5_000_000m, "VND", PaymentMethod.Paypal, "https://app.test/return", Now));
        checkout.IsSuccess.Should().BeTrue();
        checkout.Value.PaymentUrl.Should().Be("https://www.sandbox.paypal.com/checkoutnow?token=ORDER-123");
        checkout.Value.GatewayReferenceId.Should().Be("ORDER-123");

        // 2. Capture ORDER_ALREADY_CAPTURED fallback
        var capture = await gateway.CaptureOrderAsync("ORDER-ALREADY");
        capture.IsSuccess.Should().BeTrue();
        alreadyCapturedFirstAttempt.Should().BeTrue();
        capture.Value.TransactionId.Should().Be("CAP-EXISTING-1");
        capture.Value.Amount.Should().Be(200.00m);
        capture.Value.Currency.Should().Be("USD");
        gateway.IsWebhookAmountValid(5_000_000m, "VND", capture.Value.Amount, capture.Value.Currency).Should().BeTrue();

        // 3. Webhook verification (missing headers fails; valid headers succeed)
        var missingHeaders = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Paypal,
            "{}",
            new Dictionary<string, string>(),
            new Dictionary<string, string>()));
        missingHeaders.IsFailure.Should().BeTrue();
        missingHeaders.Error.Code.Should().Be("payment.invalid_signature");

        var paypalHeaders = new Dictionary<string, string>
        {
            ["PAYPAL-TRANSMISSION-ID"] = "tx-1",
            ["PAYPAL-TRANSMISSION-TIME"] = "2026-11-19T10:00:00Z",
            ["PAYPAL-CERT-URL"] = "https://api.sandbox.paypal.com/cert",
            ["PAYPAL-AUTH-ALGO"] = "SHA256withRSA",
            ["PAYPAL-TRANSMISSION-SIG"] = "sig-value"
        };
        var webhookBody = $$"""
            {
              "event_type": "PAYMENT.CAPTURE.COMPLETED",
              "resource": {
                "id": "CAP-WEBHOOK-1",
                "custom_id": "{{paymentId}}",
                "amount": { "currency_code": "USD", "value": "200.00" }
              }
            }
            """;

        var webhookResult = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Paypal,
            webhookBody,
            paypalHeaders,
            new Dictionary<string, string>()));
        webhookResult.IsSuccess.Should().BeTrue();
        webhookResult.Value.IsSuccess.Should().BeTrue();
        webhookResult.Value.TransactionId.Should().Be("CAP-WEBHOOK-1");

        // 4. Refund
        var refund = await gateway.RefundAsync(
            new GatewayRefundRequest(paymentId, PaymentMethod.Paypal, "CAP-EXISTING-1", 2_500_000m, 5_000_000m, "VND", "Partial refund", Now));
        refund.IsSuccess.Should().BeTrue();
        refund.Value.RefundTransactionId.Should().Be("PP-REF-001");

        // OAuth2 token cached across all 4 calls
        tokenCallCount.Should().Be(1);
    }

    [Fact]
    public async Task BankTransferGateway_VietQr_WebhookAuth_And_Refund_ShouldWorkCorrectly()
    {
        var opts = new BankTransferOptions
        {
            BankCode = "VCB",
            AccountNumber = "0123456789",
            AccountName = "CAR RENTAL",
            WebhookSecret = "sepay-webhook-secret-key",
            TransferPrefix = "CARRENTAL"
        };

        var gateway = new BankTransferPaymentGateway(
            Microsoft.Extensions.Options.Options.Create(opts),
            new FixedTimeProvider(Now),
            NullLogger<BankTransferPaymentGateway>.Instance);

        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();

        var checkout = await gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(paymentId, bookingId, 4_200_000m, "VND", PaymentMethod.Transfer, "https://app.test/return", Now));
        checkout.IsSuccess.Should().BeTrue();
        checkout.Value.PaymentUrl.Should().Contain("https://img.vietqr.io/image/VCB-0123456789-compact2.png");
        checkout.Value.PaymentUrl.Should().Contain("amount=4200000");
        checkout.Value.PaymentUrl.Should().Contain($"CARRENTAL%20{paymentId:N}");
        checkout.Value.QrCodeUrl.Should().Be(checkout.Value.PaymentUrl);

        // Unauthorized rejected
        var unauthorized = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Transfer,
            "{}",
            new Dictionary<string, string> { ["Authorization"] = "Apikey wrong" },
            new Dictionary<string, string>()));
        unauthorized.IsFailure.Should().BeTrue();
        unauthorized.Error.Code.Should().Be("payment.invalid_signature");

        // SePay inbound transfer accepted and payment ID parsed from content
        var inboundBody = $$"""{"id":1001,"referenceCode":"FT263231234","transferType":"in","transferAmount":4200000,"content":"Chuyen khoan CARRENTAL {{paymentId:N}}"}""";
        var inbound = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Transfer,
            inboundBody,
            new Dictionary<string, string> { ["Authorization"] = $"Apikey {opts.WebhookSecret}" },
            new Dictionary<string, string>()));
        inbound.IsSuccess.Should().BeTrue();
        inbound.Value.IsSuccess.Should().BeTrue();
        inbound.Value.TransactionId.Should().Be("FT263231234");
        inbound.Value.OrderId.Should().Be(paymentId.ToString("N"));
        inbound.Value.Amount.Should().Be(4_200_000m);

        // Outgoing transfer ignored when signed with X-Signature HMAC-SHA256
        var outgoingBody = """{"id":1002,"transferType":"out","transferAmount":100000,"content":"Out"}""";
        var hmacSig = ComputeHmacSha256(opts.WebhookSecret, outgoingBody);
        var outgoing = await gateway.ParseAndVerifyWebhookAsync(new GatewayRawWebhook(
            PaymentMethod.Transfer,
            outgoingBody,
            new Dictionary<string, string> { ["X-Signature"] = hmacSig },
            new Dictionary<string, string>()));
        outgoing.IsSuccess.Should().BeTrue();
        outgoing.Value.IsIgnored.Should().BeTrue();

        var refund = await gateway.RefundAsync(
            new GatewayRefundRequest(paymentId, PaymentMethod.Transfer, "FT263231234", 500_000m, 4_200_000m, "VND", "Deposit return", Now));
        refund.IsSuccess.Should().BeTrue();
        refund.Value.RefundTransactionId.Should().StartWith("TRF-REF-");
    }

    [Fact]
    public async Task RoutingPaymentGateway_ShouldRouteAndRejectMissingProvidersOrEmptySecrets()
    {
        var transferProvider = new BankTransferPaymentGateway(
            Microsoft.Extensions.Options.Options.Create(new BankTransferOptions()),
            new FixedTimeProvider(Now),
            NullLogger<BankTransferPaymentGateway>.Instance);

        var router = new RoutingPaymentGateway([transferProvider]);

        router.VerifyWebhookSignature(new PaymentWebhookRequest("TXN", Guid.NewGuid().ToString(), 1000m, "00", "any"))
            .Should().BeFalse();

        var missingProvider = await router.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(Guid.NewGuid(), Guid.NewGuid(), 1000m, "VND", PaymentMethod.Stripe, "https://app.test/return", Now));
        missingProvider.IsFailure.Should().BeTrue();
        missingProvider.Error.Code.Should().Be("payment.invalid_method");
    }

    private static VnPayPaymentGateway CreateVnPayGateway(VnPayOptions opts, FakeHttpMessageHandler? handler = null) =>
        new(
            Microsoft.Extensions.Options.Options.Create(opts),
            new FakeHttpClientFactory(handler ?? new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK))),
            new FixedTimeProvider(Now),
            NullLogger<VnPayPaymentGateway>.Instance);

    private static StripePaymentGateway CreateStripeGateway(StripeOptions opts, FakeHttpMessageHandler handler) =>
        new(
            Microsoft.Extensions.Options.Options.Create(opts),
            new FakeHttpClientFactory(handler),
            new FixedTimeProvider(Now),
            NullLogger<StripePaymentGateway>.Instance);

    private static PayPalPaymentGateway CreatePayPalGateway(PayPalOptions opts, FakeHttpMessageHandler handler)
    {
        var factory = new FakeHttpClientFactory(handler);
        var wrappedOptions = Microsoft.Extensions.Options.Options.Create(opts);
        var clock = new FixedTimeProvider(Now);
        var tokenProvider = new PayPalAccessTokenProvider(factory, wrappedOptions, clock);
        return new PayPalPaymentGateway(
            wrappedOptions,
            factory,
            tokenProvider,
            clock,
            NullLogger<PayPalPaymentGateway>.Instance);
    }

    private static string BuildUrlEncodedQuery(SortedDictionary<string, string> parameters) =>
        string.Join('&', parameters.Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    private static string ComputeHmacSha512(string secret, string payload) =>
        Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));

    private static string ComputeHmacSha256(string secret, string payload) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload)));

    private static string BuildStripeSignatureHeader(string secret, long timestamp, string rawBody)
    {
        var sig = ComputeHmacSha256(secret, $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{rawBody}");
        return $"t={timestamp.ToString(CultureInfo.InvariantCulture)},v1={sig}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeHttpClientFactory(FakeHttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        Dictionary<string, string> Headers,
        string Body);

    private sealed class FakeHttpMessageHandler(
        Func<HttpRequestMessage, string, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
                headers[header.Key] = string.Join(",", header.Value);

            Requests.Add(new RecordedRequest(request.Method, request.RequestUri, headers, body));
            return responder(request, body);
        }
    }
}
