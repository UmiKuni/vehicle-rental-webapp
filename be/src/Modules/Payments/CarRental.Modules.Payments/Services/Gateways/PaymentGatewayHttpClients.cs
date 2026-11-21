using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Errors;
using CarRental.Modules.Payments.Options;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Payments.Services.Gateways;

internal static class PaymentGatewayHttpClients
{
    public const string VnPay = "Payments.VNPay";
    public const string PayPal = "Payments.PayPal";
    public const string Stripe = "Payments.Stripe";
}

internal sealed class PayPalAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<PayPalOptions> options,
    TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedToken;
    private string? _cacheKey;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public async Task<Result<string>> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId)
            || string.IsNullOrWhiteSpace(opts.ClientSecret)
            || string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            return PaymentsErrors.GatewayNotConfigured("PayPal");
        }

        var clientId = opts.ClientId.Trim();
        var clientSecret = opts.ClientSecret.Trim();
        var baseUrl = opts.BaseUrl.Trim().TrimEnd('/');
        var currentKey = $"{baseUrl}|{clientId}|{clientSecret}";
        var now = clock.GetUtcNow();

        if (_cachedToken is not null && string.Equals(_cacheKey, currentKey, StringComparison.Ordinal) && _expiresAt > now)
            return _cachedToken;

        await _lock.WaitAsync(ct);
        try
        {
            now = clock.GetUtcNow();
            if (_cachedToken is not null && string.Equals(_cacheKey, currentKey, StringComparison.Ordinal) && _expiresAt > now)
                return _cachedToken;

            var client = httpClientFactory.CreateClient(PaymentGatewayHttpClients.PayPal);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
            var basicCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicCredentials);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("grant_type", "client_credentials")]);

            using var response = await client.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return PaymentsErrors.GatewayRequestFailed("PayPal", $"OAuth token request returned HTTP {(int)response.StatusCode}: {ExtractErrorDetail(body)}");

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("access_token", out var tokenProp)
                || string.IsNullOrWhiteSpace(tokenProp.GetString()))
            {
                return PaymentsErrors.GatewayRequestFailed("PayPal", "OAuth token response did not include access_token.");
            }

            var accessToken = tokenProp.GetString()!;
            var expiresInSeconds = doc.RootElement.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var exp)
                ? exp
                : 300;

            var ttlSeconds = expiresInSeconds > 60 ? expiresInSeconds - 60 : Math.Max(1, expiresInSeconds / 2);
            _cachedToken = accessToken;
            _cacheKey = currentKey;
            _expiresAt = clock.GetUtcNow().AddSeconds(ttlSeconds);

            return accessToken;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return PaymentsErrors.GatewayRequestFailed("PayPal", ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private static string ExtractErrorDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Empty response.";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var desc) && !string.IsNullOrWhiteSpace(desc.GetString()))
                return desc.GetString()!;
            if (doc.RootElement.TryGetProperty("message", out var msg) && !string.IsNullOrWhiteSpace(msg.GetString()))
                return msg.GetString()!;
            if (doc.RootElement.TryGetProperty("error", out var err) && !string.IsNullOrWhiteSpace(err.GetString()))
                return err.GetString()!;
        }
        catch (JsonException)
        {
            // Fall back to raw response body below.
        }

        return body.Length > 300 ? body[..300] : body;
    }
}
