using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
/// VNPay payment gateway integration (expects Payments:VNPay:* configuration: TmnCode, HashSecret, BaseUrl, ApiUrl).
/// </summary>
internal sealed class VnPayPaymentGateway(
    IOptions<VnPayOptions> options,
    IHttpClientFactory httpClientFactory,
    TimeProvider clock,
    ILogger<VnPayPaymentGateway> logger) : IPaymentGatewayProvider
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public PaymentMethod Method => PaymentMethod.VNPay;

    public Task<Result<GatewayCheckoutResult>> CreateCheckoutSessionAsync(
        GatewayCheckoutRequest request,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.TmnCode)
            || string.IsNullOrWhiteSpace(opts.HashSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return Task.FromResult(Result.Failure<GatewayCheckoutResult>(
                PaymentsErrors.GatewayNotConfigured("VNPay")));
        }

        var ttlMinutes = opts.CheckoutTtlMinutes > 0 ? opts.CheckoutTtlMinutes : 15;
        var expiresAt = request.Now.AddMinutes(ttlMinutes);
        var createDate = request.Now.ToOffset(VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var expireDate = expiresAt.ToOffset(VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var amountVnd = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero);
        var txnRef = request.PaymentId.ToString("N");
        var orderInfo = GatewayCrypto.SanitizeAscii($"Thanh toan don thue xe {request.BookingId:N}", 255);
        var clientIp = GatewayCrypto.NormalizeClientIp(request.ClientIpAddress, opts.DefaultIpAddress);

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = string.IsNullOrWhiteSpace(opts.Version) ? "2.1.0" : opts.Version.Trim(),
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = opts.TmnCode.Trim(),
            ["vnp_Amount"] = (amountVnd * 100).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = string.IsNullOrWhiteSpace(opts.CurrCode) ? "VND" : opts.CurrCode.Trim(),
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = string.IsNullOrWhiteSpace(opts.OrderType) ? "other" : opts.OrderType.Trim(),
            ["vnp_Locale"] = string.IsNullOrWhiteSpace(opts.Locale) ? "vn" : opts.Locale.Trim(),
            ["vnp_ReturnUrl"] = request.ReturnUrl,
            ["vnp_IpAddr"] = clientIp,
            ["vnp_CreateDate"] = createDate,
            ["vnp_ExpireDate"] = expireDate
        };

        var queryString = BuildSortedQueryString(parameters);
        var secureHash = GatewayCrypto.ComputeHmacSha512Hex(opts.HashSecret.Trim(), queryString);
        var baseUrl = opts.BaseUrl.Trim();
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var paymentUrl = $"{baseUrl}{separator}{queryString}&vnp_SecureHash={secureHash}";

        return Task.FromResult(Result.Success(new GatewayCheckoutResult(
            paymentUrl,
            string.Empty,
            expiresAt,
            txnRef)));
    }

    public bool VerifyWebhookSignature(PaymentWebhookRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Signature))
            return false;

        var secret = options.Value.HashSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var payload = GatewayCrypto.BuildUnifiedWebhookCanonicalPayload(request);
        var expected = GatewayCrypto.ComputeHmacSha512Hex(secret.Trim(), payload);
        return GatewayCrypto.FixedTimeEqualsHex(request.Signature, expected);
    }

    public Task<Result<GatewayWebhookNotification>> ParseAndVerifyWebhookAsync(
        GatewayRawWebhook webhook,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.HashSecret) || string.IsNullOrWhiteSpace(opts.TmnCode))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(webhook.QueryParameters, "vnp_SecureHash", out var receivedHash))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        var signedParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in webhook.QueryParameters)
        {
            if (string.IsNullOrEmpty(pair.Value)
                || !pair.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pair.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            signedParams[pair.Key] = pair.Value;
        }

        if (signedParams.Count == 0)
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        var signData = BuildSortedQueryString(signedParams);
        var expectedHash = GatewayCrypto.ComputeHmacSha512Hex(opts.HashSecret.Trim(), signData);
        if (!GatewayCrypto.FixedTimeEqualsHex(receivedHash, expectedHash))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_TmnCode", out var tmnCode)
            || !GatewayCrypto.FixedTimeEquals(tmnCode.Trim(), opts.TmnCode.Trim()))
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.InvalidSignature));
        }

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_TxnRef", out var txnRef))
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(PaymentsErrors.WebhookInvalidOrder));

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_Amount", out var rawAmountText)
            || !long.TryParse(rawAmountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawAmount)
            || rawAmount <= 0)
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                PaymentsErrors.WebhookPayloadInvalid("Missing or invalid vnp_Amount.")));
        }

        if (!GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_ResponseCode", out var responseCode))
        {
            return Task.FromResult(Result.Failure<GatewayWebhookNotification>(
                PaymentsErrors.WebhookPayloadInvalid("Missing vnp_ResponseCode.")));
        }

        GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_TransactionStatus", out var txnStatus);
        GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_TransactionNo", out var txnNo);
        GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_PayDate", out var payDate);
        GatewayCrypto.TryGetCaseInsensitiveValue(signedParams, "vnp_CurrCode", out var currency);

        var transactionId = string.IsNullOrWhiteSpace(txnNo) || string.Equals(txnNo.Trim(), "0", StringComparison.Ordinal)
            ? $"VNPAY-{txnRef.Trim()}-{(string.IsNullOrWhiteSpace(payDate) ? clock.GetUtcNow().ToOffset(VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) : payDate.Trim())}"
            : txnNo.Trim();

        var isSuccess = string.Equals(responseCode.Trim(), "00", StringComparison.Ordinal)
            && (string.IsNullOrWhiteSpace(txnStatus) || string.Equals(txnStatus.Trim(), "00", StringComparison.Ordinal));

        var rawPayloadJson = JsonSerializer.Serialize(signedParams);
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? "VND" : currency.Trim().ToUpperInvariant();

        return Task.FromResult(Result.Success(new GatewayWebhookNotification(
            PaymentMethod.VNPay,
            transactionId,
            txnRef.Trim(),
            rawAmount / 100m,
            normalizedCurrency,
            isSuccess,
            responseCode.Trim(),
            rawPayloadJson)));
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
        if (string.IsNullOrWhiteSpace(opts.TmnCode)
            || string.IsNullOrWhiteSpace(opts.HashSecret)
            || string.IsNullOrWhiteSpace(opts.ApiUrl))
        {
            return PaymentsErrors.GatewayNotConfigured("VNPay");
        }

        var now = clock.GetUtcNow();
        var requestId = Guid.CreateVersion7().ToString("N");
        var version = string.IsNullOrWhiteSpace(opts.Version) ? "2.1.0" : opts.Version.Trim();
        const string command = "refund";
        var tmnCode = opts.TmnCode.Trim();
        var transactionType = request.Amount >= request.OriginalAmount ? "02" : "03";
        var txnRef = request.PaymentId.ToString("N");
        var amountMinor = ((long)Math.Round(request.Amount, MidpointRounding.AwayFromZero) * 100)
            .ToString(CultureInfo.InvariantCulture);
        var transactionNo = request.GatewayTransactionId.Trim();
        var transactionDate = (request.PaymentCompletedAt ?? now)
            .ToOffset(VietnamOffset)
            .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        const string createBy = "system";
        var createDate = now.ToOffset(VietnamOffset).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var ipAddr = string.IsNullOrWhiteSpace(opts.DefaultIpAddress) ? "127.0.0.1" : opts.DefaultIpAddress.Trim();
        var orderInfo = GatewayCrypto.SanitizeAscii($"Hoan tien {txnRef} {request.Reason}", 200);

        var hashData = string.Join('|',
            requestId,
            version,
            command,
            tmnCode,
            transactionType,
            txnRef,
            amountMinor,
            transactionNo,
            transactionDate,
            createBy,
            createDate,
            ipAddr,
            orderInfo);

        var secureHash = GatewayCrypto.ComputeHmacSha512Hex(opts.HashSecret.Trim(), hashData);

        var payload = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = version,
            ["vnp_Command"] = command,
            ["vnp_TmnCode"] = tmnCode,
            ["vnp_TransactionType"] = transactionType,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_Amount"] = amountMinor,
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_TransactionNo"] = transactionNo,
            ["vnp_TransactionDate"] = transactionDate,
            ["vnp_CreateBy"] = createBy,
            ["vnp_CreateDate"] = createDate,
            ["vnp_IpAddr"] = ipAddr,
            ["vnp_SecureHash"] = secureHash
        };

        try
        {
            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.VnPay);
            using var response = await client.PostAsJsonAsync(opts.ApiUrl.Trim(), payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("VNPay refund request failed with HTTP {StatusCode} for Payment {PaymentId}", (int)response.StatusCode, request.PaymentId);
                return PaymentsErrors.GatewayRequestFailed("VNPay", $"HTTP {(int)response.StatusCode}: {Truncate(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var respCode = root.TryGetProperty("vnp_ResponseCode", out var codeProp) ? codeProp.GetString() : null;
            if (!string.Equals(respCode, "00", StringComparison.Ordinal))
            {
                var respMsg = root.TryGetProperty("vnp_Message", out var msgProp) ? msgProp.GetString() : body;
                return PaymentsErrors.GatewayRequestFailed("VNPay", $"ResponseCode={respCode ?? "unknown"}: {respMsg}");
            }

            var refundTxnNo = root.TryGetProperty("vnp_TransactionNo", out var txnProp) && !string.IsNullOrWhiteSpace(txnProp.GetString())
                ? txnProp.GetString()!
                : requestId;

            return new GatewayRefundResult(refundTxnNo, request.Amount, now);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "VNPay refund request errored for Payment {PaymentId}", request.PaymentId);
            return PaymentsErrors.GatewayRequestFailed("VNPay", ex.Message);
        }
    }

    private static string BuildSortedQueryString(SortedDictionary<string, string> parameters) =>
        string.Join('&', parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    private static string Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Empty response.";
        return value.Length > 300 ? value[..300] : value;
    }
}
