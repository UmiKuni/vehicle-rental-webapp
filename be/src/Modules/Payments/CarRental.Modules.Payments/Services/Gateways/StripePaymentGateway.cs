using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Errors;
using CarRental.Modules.Payments.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Stripe payment gateway integration (expects Payments:Stripe:* configuration: SecretKey, PublishableKey, WebhookSecret, ApiBaseUrl).
/// </summary>
public sealed class StripePaymentGateway(
    IOptions<StripeOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider clock,
    ILogger<StripePaymentGateway> logger) : IPaymentGatewayProvider
{
    private static readonly HashSet<string> ZeroDecimalCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "bif", "clp", "djf", "gnf", "jpy", "kmf", "krw",
        "mga", "pyg", "rwf", "ugx", "vnd", "vuv", "xaf", "xof", "xpf"
    };

    public PaymentMethod Method => PaymentMethod.Stripe;

    public async Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.SecretKey) || string.IsNullOrWhiteSpace(opts.ApiBaseUrl))
            return PaymentsErrors.GatewayNotConfigured("Stripe");

        var currency = (string.IsNullOrWhiteSpace(opts.Currency) ? request.Currency : opts.Currency)
            .Trim()
            .ToLowerInvariant();
        var unitAmount = ToStripeMinorUnits(request.Amount, currency);
        var ttlMinutes = Math.Max(30, opts.CheckoutTtlMinutes);
        var expiresAt = request.Now.AddMinutes(ttlMinutes);

        var separator = request.ReturnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var successUrl = $"{request.ReturnUrl}{separator}session_id={{CHECKOUT_SESSION_ID}}";
        var baseUrl = opts.ApiBaseUrl.Trim().TrimEnd('/');

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("success_url", successUrl),
            new("cancel_url", request.ReturnUrl),
            new("client_reference_id", request.PaymentId.ToString()),
            new("expires_at", expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", currency),
            new("line_items[0][price_data][unit_amount]", unitAmount.ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", $"Car Rental Booking {request.BookingId:N}"),
            new("metadata[payment_id]", request.PaymentId.ToString()),
            new("metadata[booking_id]", request.BookingId.ToString()),
            new("payment_intent_data[metadata][payment_id]", request.PaymentId.ToString()),
            new("payment_intent_data[metadata][booking_id]", request.BookingId.ToString())
        };

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.Stripe);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/checkout/sessions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.SecretKey.Trim());
            httpRequest.Content = new FormUrlEncodedContent(form);

            using var response = await client.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = ExtractStripeError(body, (int)response.StatusCode);
                logger.LogWarning("Stripe checkout session creation failed for Payment {PaymentId}: {Error}", request.PaymentId, errorDetail);
                return PaymentsErrors.GatewayRequestFailed("Stripe", errorDetail);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var sessionId = GetStringProperty(root, "id");
            var checkoutUrl = GetStringProperty(root, "url");
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(checkoutUrl))
                return PaymentsErrors.GatewayRequestFailed("Stripe", "Checkout session response did not include session id or url.");

            var actualExpiresAt = root.TryGetProperty("expires_at", out var expProp) && expProp.TryGetInt64(out var expUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expUnix)
                : expiresAt;

            return new GatewayCheckoutResult(checkoutUrl, string.Empty, actualExpiresAt, sessionId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "Stripe checkout session request errored for Payment {PaymentId}", request.PaymentId);
            return PaymentsErrors.GatewayRequestFailed("Stripe", ex.Message);
        }
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var payload = GatewayCrypto.BuildUnifiedWebhookCanonicalPayload(request);
        var expected = GatewayCrypto.ComputeHmacSha256Hex(secret.Trim(), payload);
        return GatewayCrypto.FixedTimeEqualsHex(request.Signature, expected);
    }

    public Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.WebhookSecret))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "Stripe-Signature", out var signatureHeader))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        if (!TryParseStripeSignatureHeader(signatureHeader, out var timestamp, out var v1Signatures))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        var tolerance = opts.WebhookToleranceSeconds > 0 ? opts.WebhookToleranceSeconds : 300;
        var currentUnix = clock.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(currentUnix - timestamp) > tolerance)
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        var signedPayload = $"{timestamp.ToString(CultureInfo.InvariantCulture)}.{webhook.RawBody}";
        var expectedSig = GatewayCrypto.ComputeHmacSha256Hex(opts.WebhookSecret.Trim(), signedPayload);
        if (!v1Signatures.Any(v1 => GatewayCrypto.FixedTimeEqualsHex(v1, expectedSig)))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        try
        {
            using var doc = JsonDocument.Parse(webhook.RawBody);
            var root = doc.RootElement;
            var eventType = GetStringProperty(root, "type");
            if (string.IsNullOrWhiteSpace(eventType)
                || !root.TryGetProperty("data", out var dataElem)
                || !dataElem.TryGetProperty("object", out var objElem))
            {
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                    PaymentsErrors.WebhookPayloadInvalid("Stripe event payload is missing type or data.object.")));
            }

            return eventType switch
            {
                "checkout.session.completed" or "checkout.session.async_payment_succeeded" =>
                    Task.FromResult(ParseCheckoutSessionEvent(objElem, eventType, isSuccessEvent: true, webhook.RawBody, opts)),
                "checkout.session.expired" or "checkout.session.async_payment_failed" =>
                    Task.FromResult(ParseCheckoutSessionEvent(objElem, eventType, isSuccessEvent: false, webhook.RawBody, opts)),
                "payment_intent.payment_failed" =>
                    Task.FromResult(ParsePaymentIntentFailedEvent(objElem, eventType, webhook.RawBody, opts)),
                _ => Task.FromResult(Result.Success(new GatewayWebhookNotification(
                    PaymentMethod.Stripe,
                    string.Empty,
                    string.Empty,
                    0m,
                    string.Empty,
                    false,
                    eventType,
                    webhook.RawBody,
                    IsIgnored: true,
                    IgnoredReason: $"Ignored Stripe webhook event '{eventType}'.")))
            };
        }
        catch (JsonException)
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                PaymentsErrors.WebhookPayloadInvalid("Invalid Stripe webhook JSON payload.")));
        }
    }

    public bool IsWebhookAmountValid(
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency) =>
        webhookAmount == expectedPaymentAmount
        && string.Equals(webhookCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase);

    public async Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.SecretKey) || string.IsNullOrWhiteSpace(opts.ApiBaseUrl))
            return PaymentsErrors.GatewayNotConfigured("Stripe");

        var txnId = request.GatewayTransactionId.Trim();
        string targetParam;
        if (txnId.StartsWith("pi_", StringComparison.Ordinal))
            targetParam = "payment_intent";
        else if (txnId.StartsWith("ch_", StringComparison.Ordinal))
            targetParam = "charge";
        else
            return PaymentsErrors.GatewayRequestFailed(
                "Stripe",
                "Unsupported gateway transaction ID format; expected Stripe PaymentIntent (pi_...) or Charge (ch_...).");

        var currency = (string.IsNullOrWhiteSpace(opts.Currency) ? request.Currency : opts.Currency)
            .Trim()
            .ToLowerInvariant();
        var refundMinorUnits = ToStripeMinorUnits(request.Amount, currency);
        var truncatedReason = request.Reason.Length > 500 ? request.Reason[..500] : request.Reason;
        var baseUrl = opts.ApiBaseUrl.Trim().TrimEnd('/');

        var form = new List<KeyValuePair<string, string>>
        {
            new(targetParam, txnId),
            new("amount", refundMinorUnits.ToString(CultureInfo.InvariantCulture)),
            new("reason", "requested_by_customer"),
            new("metadata[payment_id]", request.PaymentId.ToString()),
            new("metadata[reason]", truncatedReason)
        };

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.Stripe);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/refunds");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.SecretKey.Trim());
            httpRequest.Headers.TryAddWithoutValidation(
                "Idempotency-Key",
                $"refund-{request.PaymentId:N}-{Guid.CreateVersion7():N}");
            httpRequest.Content = new FormUrlEncodedContent(form);

            using var response = await client.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorDetail = ExtractStripeError(body, (int)response.StatusCode);
                logger.LogWarning("Stripe refund failed for Payment {PaymentId}: {Error}", request.PaymentId, errorDetail);
                return PaymentsErrors.GatewayRequestFailed("Stripe", errorDetail);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var refundId = GetStringProperty(root, "id");
            if (string.IsNullOrWhiteSpace(refundId))
                return PaymentsErrors.GatewayRequestFailed("Stripe", "Refund response did not include refund id.");

            var createdAt = root.TryGetProperty("created", out var createdProp) && createdProp.TryGetInt64(out var createdUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(createdUnix)
                : clock.GetUtcNow();

            return new GatewayRefundResult(refundId, request.Amount, createdAt);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "Stripe refund request errored for Payment {PaymentId}", request.PaymentId);
            return PaymentsErrors.GatewayRequestFailed("Stripe", ex.Message);
        }
    }

    private static Result<GatewayWebhookNotification> ParseCheckoutSessionEvent(
        JsonElement obj,
        string eventType,
        bool isSuccessEvent,
        string rawBody,
        StripeOptions opts)
    {
        var paymentStatus = GetStringProperty(obj, "payment_status");
        if (isSuccessEvent && !string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
        {
            return new GatewayWebhookNotification(
                PaymentMethod.Stripe,
                string.Empty,
                string.Empty,
                0m,
                string.Empty,
                false,
                eventType,
                rawBody,
                IsIgnored: true,
                IgnoredReason: $"Stripe checkout session payment_status is '{paymentStatus}'.");
        }

        var orderId = GetMetadataValue(obj, "payment_id") ?? GetStringProperty(obj, "client_reference_id");
        if (string.IsNullOrWhiteSpace(orderId))
            return PaymentsErrors.WebhookInvalidOrder;

        var transactionId = GetStringProperty(obj, "payment_intent") ?? GetStringProperty(obj, "id");
        if (string.IsNullOrWhiteSpace(transactionId))
            return PaymentsErrors.InvalidTransactionId;

        var currency = (GetStringProperty(obj, "currency") ?? opts.Currency).Trim().ToUpperInvariant();
        if (!obj.TryGetProperty("amount_total", out var amountProp) || !amountProp.TryGetInt64(out var amountTotal))
            return PaymentsErrors.WebhookPayloadInvalid("Stripe checkout session is missing amount_total.");

        var amount = FromStripeMinorUnits(amountTotal, currency);
        return new GatewayWebhookNotification(
            PaymentMethod.Stripe,
            transactionId,
            orderId,
            amount,
            currency,
            isSuccessEvent,
            isSuccessEvent ? "00" : eventType,
            rawBody);
    }

    private static Result<GatewayWebhookNotification> ParsePaymentIntentFailedEvent(
        JsonElement obj,
        string eventType,
        string rawBody,
        StripeOptions opts)
    {
        var orderId = GetMetadataValue(obj, "payment_id");
        if (string.IsNullOrWhiteSpace(orderId))
            return PaymentsErrors.WebhookInvalidOrder;

        var transactionId = GetStringProperty(obj, "id");
        if (string.IsNullOrWhiteSpace(transactionId))
            return PaymentsErrors.InvalidTransactionId;

        var currency = (GetStringProperty(obj, "currency") ?? opts.Currency).Trim().ToUpperInvariant();
        if (!obj.TryGetProperty("amount", out var amountProp) || !amountProp.TryGetInt64(out var minorAmount))
            return PaymentsErrors.WebhookPayloadInvalid("Stripe payment_intent is missing amount.");

        var failureCode = eventType;
        if (obj.TryGetProperty("last_payment_error", out var errObj)
            && errObj.ValueKind == JsonValueKind.Object)
        {
            var code = GetStringProperty(errObj, "code");
            if (!string.IsNullOrWhiteSpace(code))
                failureCode = code;
        }

        return new GatewayWebhookNotification(
            PaymentMethod.Stripe,
            transactionId,
            orderId,
            FromStripeMinorUnits(minorAmount, currency),
            currency,
            false,
            failureCode,
            rawBody);
    }

    private static bool TryParseStripeSignatureHeader(
        string header,
        out long timestamp,
        out List<string> v1Signatures)
    {
        timestamp = 0;
        v1Signatures = [];
        var hasTimestamp = false;

        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eqIdx = part.IndexOf('=', StringComparison.Ordinal);
            if (eqIdx <= 0 || eqIdx == part.Length - 1)
                continue;

            var key = part[..eqIdx].Trim();
            var value = part[(eqIdx + 1)..].Trim();

            if (string.Equals(key, "t", StringComparison.Ordinal))
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out timestamp))
                    hasTimestamp = true;
            }
            else if (string.Equals(key, "v1", StringComparison.Ordinal) && !string.IsNullOrEmpty(value))
            {
                v1Signatures.Add(value);
            }
        }

        return hasTimestamp && v1Signatures.Count > 0;
    }

    private static long ToStripeMinorUnits(decimal amount, string currency) =>
        ZeroDecimalCurrencies.Contains(currency)
            ? (long)Math.Round(amount, MidpointRounding.AwayFromZero)
            : (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static decimal FromStripeMinorUnits(long minorUnits, string currency) =>
        ZeroDecimalCurrencies.Contains(currency)
            ? minorUnits
            : minorUnits / 100m;

    private static string? GetMetadataValue(JsonElement obj, string key)
    {
        if (obj.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
            return GetStringProperty(metadata, key);
        return null;
    }

    private static string? GetStringProperty(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string ExtractStripeError(string body, int statusCode)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var err)
                    && err.ValueKind == JsonValueKind.Object
                    && err.TryGetProperty("message", out var msg)
                    && !string.IsNullOrWhiteSpace(msg.GetString()))
                {
                    return $"HTTP {statusCode}: {msg.GetString()}";
                }
            }
            catch (JsonException)
            {
                // Fall back to truncated body below.
            }
        }

        var detail = string.IsNullOrWhiteSpace(body) ? "Empty response." : (body.Length > 300 ? body[..300] : body);
        return $"HTTP {statusCode}: {detail}";
    }
}
