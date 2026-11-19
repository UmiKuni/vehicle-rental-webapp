using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
/// PayPal payment gateway integration (expects Payments:PayPal:* configuration: ClientId, ClientSecret, WebhookId, BaseUrl).
/// </summary>
internal sealed class PayPalPaymentGateway(
    IOptions<PayPalOptions> options,
    IHttpClientFactory httpClientFactory,
    PayPalAccessTokenProvider tokenProvider,
    TimeProvider clock,
    ILogger<PayPalPaymentGateway> logger) : IPaymentGatewayProvider, IPayPalOrderCaptureProvider
{
    public PaymentMethod Method => PaymentMethod.Paypal;

    public async Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return PaymentsErrors.GatewayNotConfigured("PayPal");
        }

        if (!TryConvertAmount(request.Amount, opts, out var targetCurrency, out var convertedAmount, out var formattedValue))
            return PaymentsErrors.GatewayNotConfigured("PayPal");

        if (convertedAmount < 0.01m)
            return PaymentsErrors.InvalidAmount;

        var tokenResult = await tokenProvider.GetAccessTokenAsync(ct);
        if (tokenResult.IsFailure)
            return tokenResult.Error;

        var baseUrl = opts.BaseUrl.Trim().TrimEnd('/');
        var ttlMinutes = opts.CheckoutTtlMinutes > 0 ? opts.CheckoutTtlMinutes : 15;
        var expiresAt = request.Now.AddMinutes(ttlMinutes);
        var brandName = string.IsNullOrWhiteSpace(opts.BrandName) ? "CarRental Platform" : opts.BrandName.Trim();

        var orderPayload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = request.BookingId.ToString("N"),
                    custom_id = request.PaymentId.ToString(),
                    description = $"Car Rental Booking {request.BookingId:N}",
                    amount = new
                    {
                        currency_code = targetCurrency,
                        value = formattedValue
                    }
                }
            },
            payment_source = new
            {
                paypal = new
                {
                    experience_context = new
                    {
                        brand_name = brandName,
                        user_action = "PAY_NOW",
                        return_url = request.ReturnUrl,
                        cancel_url = request.ReturnUrl
                    }
                }
            }
        };

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.PayPal);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            httpRequest.Headers.TryAddWithoutValidation(
                "PayPal-Request-Id",
                $"{request.PaymentId:N}-{request.Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var detail = ExtractPayPalError(body, (int)response.StatusCode);
                logger.LogWarning("PayPal order creation failed for Payment {PaymentId}: {Error}", request.PaymentId, detail);
                return PaymentsErrors.GatewayRequestFailed("PayPal", detail);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var orderId = GetStringProperty(root, "id");
            var approveUrl = ExtractApproveUrl(root);

            if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(approveUrl))
                return PaymentsErrors.GatewayRequestFailed("PayPal", "PayPal order response did not include id or approval link.");

            return new GatewayCheckoutResult(approveUrl, string.Empty, expiresAt, orderId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "PayPal order creation errored for Payment {PaymentId}", request.PaymentId);
            return PaymentsErrors.GatewayRequestFailed("PayPal", ex.Message);
        }
    }

    public async Task<Result<GatewayWebhookNotification>> CaptureOrderAsync(
        string paypalOrderId,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return PaymentsErrors.GatewayNotConfigured("PayPal");
        }

        if (string.IsNullOrWhiteSpace(paypalOrderId))
            return PaymentsErrors.InvalidPayPalOrderId;

        var tokenResult = await tokenProvider.GetAccessTokenAsync(ct);
        if (tokenResult.IsFailure)
            return tokenResult.Error;

        var baseUrl = opts.BaseUrl.Trim().TrimEnd('/');
        var encodedOrderId = Uri.EscapeDataString(paypalOrderId.Trim());

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.PayPal);
            using var captureRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v2/checkout/orders/{encodedOrderId}/capture");
            captureRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            captureRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(captureRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity
                && body.Contains("ORDER_ALREADY_CAPTURED", StringComparison.OrdinalIgnoreCase))
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/v2/checkout/orders/{encodedOrderId}");
                getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);

                using var getResponse = await client.SendAsync(getRequest, ct);
                body = await getResponse.Content.ReadAsStringAsync(ct);
                if (!getResponse.IsSuccessStatusCode)
                    return PaymentsErrors.PayPalCaptureFailed(ExtractPayPalError(body, (int)getResponse.StatusCode));
            }
            else if (!response.IsSuccessStatusCode)
            {
                return PaymentsErrors.PayPalCaptureFailed(ExtractPayPalError(body, (int)response.StatusCode));
            }

            return ParseCapturedOrderBody(body);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "PayPal order capture errored for PayPal Order {OrderId}", paypalOrderId);
            return PaymentsErrors.PayPalCaptureFailed(ex.Message);
        }
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var secret = options.Value.ClientSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var payload = GatewayCrypto.BuildUnifiedWebhookCanonicalPayload(request);
        var expected = GatewayCrypto.ComputeHmacSha256Hex(secret.Trim(), payload);
        return GatewayCrypto.FixedTimeEqualsHex(request.Signature, expected);
    }

    public async Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.WebhookId)
            || string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return PaymentsErrors.InvalidSignature;
        }

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "PAYPAL-TRANSMISSION-ID", out var transmissionId)
            || !GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "PAYPAL-TRANSMISSION-TIME", out var transmissionTime)
            || !GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "PAYPAL-CERT-URL", out var certUrl)
            || !GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "PAYPAL-AUTH-ALGO", out var authAlgo)
            || !GatewayCrypto.TryGetCaseInsensitiveValue(webhook.Headers, "PAYPAL-TRANSMISSION-SIG", out var transmissionSig))
        {
            return PaymentsErrors.InvalidSignature;
        }

        JsonDocument webhookDoc;
        try
        {
            webhookDoc = JsonDocument.Parse(webhook.RawBody);
        }
        catch (JsonException)
        {
            return PaymentsErrors.WebhookPayloadInvalid("Invalid PayPal webhook JSON payload.");
        }

        using (webhookDoc)
        {
            var tokenResult = await tokenProvider.GetAccessTokenAsync(ct);
            if (tokenResult.IsFailure)
                return tokenResult.Error;

            var baseUrl = opts.BaseUrl.Trim().TrimEnd('/');
            var verifyPayload = new Dictionary<string, object?>
            {
                ["auth_algo"] = authAlgo.Trim(),
                ["cert_url"] = certUrl.Trim(),
                ["transmission_id"] = transmissionId.Trim(),
                ["transmission_sig"] = transmissionSig.Trim(),
                ["transmission_time"] = transmissionTime.Trim(),
                ["webhook_id"] = opts.WebhookId.Trim(),
                ["webhook_event"] = webhookDoc.RootElement.Clone()
            };

            try
            {
                var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.PayPal);
                using var verifyRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{baseUrl}/v1/notifications/verify-webhook-signature");
                verifyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
                verifyRequest.Content = new StringContent(
                    JsonSerializer.Serialize(verifyPayload),
                    Encoding.UTF8,
                    "application/json");

                using var verifyResponse = await client.SendAsync(verifyRequest, ct);
                var verifyBody = await verifyResponse.Content.ReadAsStringAsync(ct);
                if (!verifyResponse.IsSuccessStatusCode)
                    return PaymentsErrors.GatewayRequestFailed("PayPal", ExtractPayPalError(verifyBody, (int)verifyResponse.StatusCode));

                using var verifyDoc = JsonDocument.Parse(verifyBody);
                var verificationStatus = GetStringProperty(verifyDoc.RootElement, "verification_status");
                if (!string.Equals(verificationStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    return PaymentsErrors.InvalidSignature;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return PaymentsErrors.GatewayRequestFailed("PayPal", ex.Message);
            }

            return ParseWebhookEvent(webhookDoc.RootElement, webhook.RawBody);
        }
    }

    public bool IsWebhookAmountValid(
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency)
    {
        if (string.Equals(webhookCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
            return webhookAmount == expectedPaymentAmount;

        var opts = options.Value;
        if (TryConvertAmount(expectedPaymentAmount, opts, out var targetCurrency, out var convertedAmount, out _)
            && string.Equals(webhookCurrency, targetCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return webhookAmount == convertedAmount;
        }

        return false;
    }

    public async Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return PaymentsErrors.GatewayNotConfigured("PayPal");
        }

        if (!TryConvertAmount(request.Amount, opts, out var targetCurrency, out var convertedAmount, out var formattedValue))
            return PaymentsErrors.GatewayNotConfigured("PayPal");

        if (convertedAmount < 0.01m)
            return PaymentsErrors.InvalidRefundAmount;

        var tokenResult = await tokenProvider.GetAccessTokenAsync(ct);
        if (tokenResult.IsFailure)
            return tokenResult.Error;

        var baseUrl = opts.BaseUrl.Trim().TrimEnd('/');
        var captureId = Uri.EscapeDataString(request.GatewayTransactionId.Trim());
        var note = string.IsNullOrWhiteSpace(request.Reason)
            ? "Refund"
            : (request.Reason.Length > 255 ? request.Reason[..255] : request.Reason);

        var refundPayload = new
        {
            amount = new
            {
                value = formattedValue,
                currency_code = targetCurrency
            },
            note_to_payer = note
        };

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.PayPal);
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{baseUrl}/v2/payments/captures/{captureId}/refund");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Value);
            httpRequest.Headers.TryAddWithoutValidation(
                "PayPal-Request-Id",
                $"{request.PaymentId:N}-{Guid.CreateVersion7():N}");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(refundPayload),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(httpRequest, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var detail = ExtractPayPalError(body, (int)response.StatusCode);
                logger.LogWarning("PayPal refund failed for Payment {PaymentId}: {Error}", request.PaymentId, detail);
                return PaymentsErrors.GatewayRequestFailed("PayPal", detail);
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var refundId = GetStringProperty(root, "id");
            var status = GetStringProperty(root, "status");
            if (string.IsNullOrWhiteSpace(refundId))
                return PaymentsErrors.GatewayRequestFailed("PayPal", "Refund response did not include refund id.");

            if (!string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "PENDING", StringComparison.OrdinalIgnoreCase))
            {
                return PaymentsErrors.GatewayRequestFailed("PayPal", $"Unexpected PayPal refund status '{status}'.");
            }

            return new GatewayRefundResult(refundId, request.Amount, clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "PayPal refund request errored for Payment {PaymentId}", request.PaymentId);
            return PaymentsErrors.GatewayRequestFailed("PayPal", ex.Message);
        }
    }

    private static Result<GatewayWebhookNotification> ParseCapturedOrderBody(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("purchase_units", out var units)
            || units.ValueKind != JsonValueKind.Array
            || units.GetArrayLength() == 0)
        {
            return PaymentsErrors.PayPalCaptureFailed("Missing purchase_units in PayPal capture response.");
        }

        var unit = units[0];
        var unitCustomId = GetStringProperty(unit, "custom_id");

        if (!unit.TryGetProperty("payments", out var payments)
            || !payments.TryGetProperty("captures", out var captures)
            || captures.ValueKind != JsonValueKind.Array
            || captures.GetArrayLength() == 0)
        {
            return PaymentsErrors.PayPalCaptureFailed("Missing capture record in PayPal response.");
        }

        var capture = captures[0];
        var captureId = GetStringProperty(capture, "id");
        var customId = GetStringProperty(capture, "custom_id") ?? unitCustomId;
        var status = GetStringProperty(capture, "status") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(captureId))
            return PaymentsErrors.InvalidTransactionId;
        if (string.IsNullOrWhiteSpace(customId))
            return PaymentsErrors.WebhookInvalidOrder;

        if (!TryExtractAmount(capture, out var amount, out var currency))
            return PaymentsErrors.PayPalCaptureFailed("Missing or invalid capture amount.");

        var isSuccess = string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase);
        return new GatewayWebhookNotification(
            PaymentMethod.Paypal,
            captureId,
            customId,
            amount,
            currency,
            isSuccess,
            isSuccess ? "00" : status,
            body);
    }

    private static Result<GatewayWebhookNotification> ParseWebhookEvent(JsonElement root, string rawBody)
    {
        var eventType = GetStringProperty(root, "event_type");
        if (string.IsNullOrWhiteSpace(eventType) || !root.TryGetProperty("resource", out var resource))
            return PaymentsErrors.WebhookPayloadInvalid("PayPal webhook payload is missing event_type or resource.");

        switch (eventType)
        {
            case "PAYMENT.CAPTURE.COMPLETED":
                return ParseCaptureWebhookResource(resource, eventType, isSuccess: true, rawBody);

            case "PAYMENT.CAPTURE.DENIED":
            case "PAYMENT.CAPTURE.DECLINED":
                return ParseCaptureWebhookResource(resource, eventType, isSuccess: false, rawBody);

            case "CHECKOUT.ORDER.APPROVED":
                var orderId = GetStringProperty(resource, "id");
                if (string.IsNullOrWhiteSpace(orderId))
                    return PaymentsErrors.WebhookPayloadInvalid("PayPal CHECKOUT.ORDER.APPROVED event is missing resource.id.");

                return new GatewayWebhookNotification(
                    PaymentMethod.Paypal,
                    orderId,
                    string.Empty,
                    0m,
                    string.Empty,
                    false,
                    eventType,
                    rawBody,
                    OrderIdToCapture: orderId);

            default:
                return new GatewayWebhookNotification(
                    PaymentMethod.Paypal,
                    string.Empty,
                    string.Empty,
                    0m,
                    string.Empty,
                    false,
                    eventType,
                    rawBody,
                    IsIgnored: true,
                    IgnoredReason: $"Ignored PayPal webhook event '{eventType}'.");
        }
    }

    private static Result<GatewayWebhookNotification> ParseCaptureWebhookResource(
        JsonElement resource,
        string eventType,
        bool isSuccess,
        string rawBody)
    {
        var captureId = GetStringProperty(resource, "id");
        if (string.IsNullOrWhiteSpace(captureId))
            return PaymentsErrors.InvalidTransactionId;

        var customId = GetStringProperty(resource, "custom_id");
        if (string.IsNullOrWhiteSpace(customId))
            return PaymentsErrors.WebhookInvalidOrder;

        if (!TryExtractAmount(resource, out var amount, out var currency))
            return PaymentsErrors.WebhookPayloadInvalid("PayPal capture resource is missing amount.");

        return new GatewayWebhookNotification(
            PaymentMethod.Paypal,
            captureId,
            customId,
            amount,
            currency,
            isSuccess,
            isSuccess ? "00" : eventType,
            rawBody);
    }

    private static bool TryExtractAmount(JsonElement obj, out decimal amount, out string currency)
    {
        amount = 0m;
        currency = string.Empty;

        if (!obj.TryGetProperty("amount", out var amountElem) || amountElem.ValueKind != JsonValueKind.Object)
            return false;

        var valueStr = GetStringProperty(amountElem, "value");
        var currencyStr = GetStringProperty(amountElem, "currency_code");

        if (string.IsNullOrWhiteSpace(valueStr)
            || string.IsNullOrWhiteSpace(currencyStr)
            || !decimal.TryParse(valueStr, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            return false;
        }

        currency = currencyStr.Trim().ToUpperInvariant();
        return true;
    }

    private static bool TryConvertAmount(
        decimal vndAmount,
        PayPalOptions opts,
        out string targetCurrency,
        out decimal convertedAmount,
        out string formattedValue)
    {
        targetCurrency = (string.IsNullOrWhiteSpace(opts.Currency) ? "USD" : opts.Currency)
            .Trim()
            .ToUpperInvariant();

        if (string.Equals(targetCurrency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            convertedAmount = Math.Round(vndAmount, 0, MidpointRounding.AwayFromZero);
            formattedValue = convertedAmount.ToString("0", CultureInfo.InvariantCulture);
            return true;
        }

        if (opts.ExchangeRate <= 0m)
        {
            convertedAmount = 0m;
            formattedValue = string.Empty;
            return false;
        }

        convertedAmount = Math.Round(vndAmount / opts.ExchangeRate, 2, MidpointRounding.AwayFromZero);
        formattedValue = convertedAmount.ToString("0.00", CultureInfo.InvariantCulture);
        return true;
    }

    private static string? ExtractApproveUrl(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return null;

        string? approveFallback = null;
        foreach (var link in links.EnumerateArray())
        {
            var rel = GetStringProperty(link, "rel");
            var href = GetStringProperty(link, "href");
            if (string.IsNullOrWhiteSpace(href))
                continue;

            if (string.Equals(rel, "payer-action", StringComparison.OrdinalIgnoreCase))
                return href;

            if (string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase))
                approveFallback = href;
        }

        return approveFallback;
    }

    private static string? GetStringProperty(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string ExtractPayPalError(string body, int statusCode)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("details", out var details)
                    && details.ValueKind == JsonValueKind.Array
                    && details.GetArrayLength() > 0)
                {
                    var first = details[0];
                    var issue = GetStringProperty(first, "issue");
                    var desc = GetStringProperty(first, "description");
                    if (!string.IsNullOrWhiteSpace(issue) || !string.IsNullOrWhiteSpace(desc))
                        return $"HTTP {statusCode}: {issue} - {desc}".Trim(' ', '-');
                }

                var msg = GetStringProperty(root, "message") ?? GetStringProperty(root, "error_description");
                if (!string.IsNullOrWhiteSpace(msg))
                    return $"HTTP {statusCode}: {msg}";
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
