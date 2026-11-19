using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Errors;
using CarRental.Modules.Payments.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Payments.Services.Gateways;

/// <summary>
/// Bank transfer / VietQR payment provider (expects Payments:Transfer:* configuration: BankCode, AccountNumber, AccountName, WebhookSecret).
/// </summary>
public sealed partial class BankTransferPaymentGateway(
    IOptions<BankTransferOptions> options,
    TimeProvider clock,
    ILogger<BankTransferPaymentGateway> logger) : IPaymentGatewayProvider
{
    [GeneratedRegex(
        @"(?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})|(?<![0-9a-fA-F])(?<hex>[0-9a-fA-F]{32})(?![0-9a-fA-F])",
        RegexOptions.Compiled)]
    private static partial Regex PaymentIdPattern();

    public PaymentMethod Method => PaymentMethod.Transfer;

    public Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BankCode)
            || string.IsNullOrWhiteSpace(opts.AccountNumber)
            || string.IsNullOrWhiteSpace(opts.AccountName))
        {
            return Task.FromResult(Result.Failure<GatewayCheckoutResult>(
                PaymentsErrors.GatewayNotConfigured("Transfer")));
        }

        var ttlMinutes = opts.CheckoutTtlMinutes > 0 ? opts.CheckoutTtlMinutes : 15;
        var expiresAt = request.Now.AddMinutes(ttlMinutes);
        var amountVnd = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero);
        var prefix = string.IsNullOrWhiteSpace(opts.TransferPrefix) ? "CARRENTAL" : opts.TransferPrefix.Trim();
        var transferContent = $"{prefix} {request.PaymentId:N}";
        var baseUrl = string.IsNullOrWhiteSpace(opts.VietQrBaseUrl)
            ? "https://img.vietqr.io/image"
            : opts.VietQrBaseUrl.Trim().TrimEnd('/');
        var template = string.IsNullOrWhiteSpace(opts.QrTemplate) ? "compact2" : opts.QrTemplate.Trim();

        var vietQrUrl =
            $"{baseUrl}/{Uri.EscapeDataString(opts.BankCode.Trim())}-{Uri.EscapeDataString(opts.AccountNumber.Trim())}-{Uri.EscapeDataString(template)}.png" +
            $"?amount={amountVnd.ToString(CultureInfo.InvariantCulture)}" +
            $"&addInfo={Uri.EscapeDataString(transferContent)}" +
            $"&accountName={Uri.EscapeDataString(opts.AccountName.Trim())}";

        return Task.FromResult(Result.Success(new GatewayCheckoutResult(
            vietQrUrl,
            vietQrUrl,
            expiresAt,
            transferContent)));
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

        if (!IsWebhookAuthorized(webhook.Headers, webhook.RawBody, opts.WebhookSecret.Trim()))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        try
        {
            using var doc = JsonDocument.Parse(webhook.RawBody);
            var root = doc.RootElement;

            var transferType = GetStringProperty(root, "transferType");
            if (!string.IsNullOrWhiteSpace(transferType)
                && !string.Equals(transferType.Trim(), "in", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(Result.Success(new GatewayWebhookNotification(
                    PaymentMethod.Transfer,
                    string.Empty,
                    string.Empty,
                    0m,
                    "VND",
                    false,
                    "IGNORED",
                    webhook.RawBody,
                    IsIgnored: true,
                    IgnoredReason: $"Ignored bank transfer webhook with transferType='{transferType}'.")));
            }

            var transactionId = GetStringOrNumberProperty(root, "referenceCode")
                ?? GetStringOrNumberProperty(root, "transactionId")
                ?? GetStringOrNumberProperty(root, "id");

            if (string.IsNullOrWhiteSpace(transactionId))
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidTransactionId));

            if (!TryGetDecimalProperty(root, "transferAmount", out var amount)
                && !TryGetDecimalProperty(root, "amount", out amount))
            {
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                    PaymentsErrors.WebhookPayloadInvalid("Bank transfer webhook is missing transferAmount or amount.")));
            }

            var orderId = GetStringProperty(root, "orderId");
            if (string.IsNullOrWhiteSpace(orderId))
            {
                var content = GetStringProperty(root, "content") ?? GetStringProperty(root, "description");
                orderId = ExtractPaymentIdFromContent(content);
            }

            if (string.IsNullOrWhiteSpace(orderId))
                return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.WebhookInvalidOrder));

            var responseCode = GetStringProperty(root, "responseCode");
            var normalizedCode = string.IsNullOrWhiteSpace(responseCode) ? "00" : responseCode.Trim();
            var isSuccess = string.Equals(normalizedCode, "00", StringComparison.Ordinal);

            return Task.FromResult(Result.Success(new GatewayWebhookNotification(
                PaymentMethod.Transfer,
                transactionId.Trim(),
                orderId.Trim(),
                amount,
                "VND",
                isSuccess,
                normalizedCode,
                webhook.RawBody)));
        }
        catch (JsonException)
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                PaymentsErrors.WebhookPayloadInvalid("Invalid bank transfer webhook JSON payload.")));
        }
    }

    public bool IsWebhookAmountValid(
        decimal expectedPaymentAmount,
        string expectedCurrency,
        decimal webhookAmount,
        string webhookCurrency) =>
        webhookAmount == expectedPaymentAmount
        && string.Equals(webhookCurrency, expectedCurrency, StringComparison.OrdinalIgnoreCase);

    public Task<Result<GatewayRefundResult>> RefundAsync(
        GatewayRefundRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.BankCode) || string.IsNullOrWhiteSpace(opts.AccountNumber))
            return Task.FromResult(Result.Failure<GatewayRefundResult>(
                PaymentsErrors.GatewayNotConfigured("Transfer")));

        var refundTxnId = $"TRF-REF-{Guid.CreateVersion7():N}";
        logger.LogInformation(
            "Recorded bank transfer refund {RefundTxnId} for Payment {PaymentId} ({Amount} {Currency}); manual bank/Napas disbursement required via {BankCode}.",
            refundTxnId,
            request.PaymentId,
            request.Amount,
            request.Currency,
            opts.BankCode);

        return Task.FromResult(Result.Success(new GatewayRefundResult(
            refundTxnId,
            request.Amount,
            clock.GetUtcNow())));
    }

    private static bool IsWebhookAuthorized(
        IReadOnlyDictionary<string, string>? headers,
        string rawBody,
        string secret)
    {
        if (GatewayCrypto.TryGetCaseInsensitiveValue(headers, "Authorization", out var authHeader))
        {
            var trimmed = authHeader.Trim();
            const string apiKeyPrefix = "Apikey ";
            const string bearerPrefix = "Bearer ";

            if (trimmed.StartsWith(apiKeyPrefix, StringComparison.OrdinalIgnoreCase)
                && GatewayCrypto.FixedTimeEquals(trimmed[apiKeyPrefix.Length..].Trim(), secret))
            {
                return true;
            }

            if (trimmed.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                && GatewayCrypto.FixedTimeEquals(trimmed[bearerPrefix.Length..].Trim(), secret))
            {
                return true;
            }
        }

        if (GatewayCrypto.TryGetCaseInsensitiveValue(headers, "X-Api-Key", out var apiKey)
            && GatewayCrypto.FixedTimeEquals(apiKey.Trim(), secret))
        {
            return true;
        }

        if (GatewayCrypto.TryGetCaseInsensitiveValue(headers, "X-Signature", out var signature))
        {
            var expected = GatewayCrypto.ComputeHmacSha256Hex(secret, rawBody);
            if (GatewayCrypto.FixedTimeEqualsHex(signature, expected))
                return true;
        }

        return false;
    }

    private static string? ExtractPaymentIdFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var match = PaymentIdPattern().Match(content);
        if (!match.Success)
            return null;

        return match.Groups["guid"].Success ? match.Groups["guid"].Value : match.Groups["hex"].Value;
    }

    private static string? GetStringProperty(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static string? GetStringOrNumberProperty(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }

    private static bool TryGetDecimalProperty(JsonElement obj, string name, out decimal value)
    {
        value = 0m;
        if (!obj.TryGetProperty(name, out var prop))
            return false;

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(prop.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }
}
