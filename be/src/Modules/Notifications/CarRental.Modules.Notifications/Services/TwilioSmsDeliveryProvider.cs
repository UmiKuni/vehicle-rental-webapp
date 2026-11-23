using System.Net.Http.Headers;
using System.Text;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Notifications.Services;

public sealed class TwilioSmsDeliveryProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<NotificationDeliveryOptions> options,
    ILogger<TwilioSmsDeliveryProvider> logger)
{
    private readonly TwilioDeliveryOptions _options = options.Value.Twilio;

    public async Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.AccountSid) ||
            string.IsNullOrWhiteSpace(_options.AuthToken))
            throw new NotificationDeliveryException("Twilio SMS delivery is not configured.");

        if (string.IsNullOrWhiteSpace(_options.FromNumber) &&
            string.IsNullOrWhiteSpace(_options.MessagingServiceSid))
            throw new NotificationDeliveryException("Twilio SMS sender is not configured.");

        if (contact is null)
            throw new NotificationDeliveryException("The notification recipient has no phone number.");

        var phone = NormalizePhoneNumber(contact.Phone);
        if (phone is null)
            throw new NotificationDeliveryException("The notification recipient has no valid phone number.");

        var accountSid = Uri.EscapeDataString(_options.AccountSid.Trim());
        var baseUrl = (_options.BaseUrl.TrimEnd('/'));
        var requestUri = $"{baseUrl}/2010-04-01/Accounts/{accountSid}/Messages.json";

        var values = new Dictionary<string, string>
        {
            ["To"] = phone,
            ["Body"] = notification.Content
        };

        if (!string.IsNullOrWhiteSpace(_options.MessagingServiceSid))
            values["MessagingServiceSid"] = _options.MessagingServiceSid.Trim();
        else
            values["From"] = _options.FromNumber.Trim();

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = new FormUrlEncodedContent(values)
        };
        var credentials = $"{_options.AccountSid.Trim()}:{_options.AuthToken.Trim()}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials)));

        var client = httpClientFactory.CreateClient("Notifications.Twilio");

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning(
                    "Twilio SMS notification {NotificationId} failed with status {StatusCode}: {Response}",
                    notification.Id.Value,
                    (int)response.StatusCode,
                    responseBody[..Math.Min(responseBody.Length, 500)]);
                throw new NotificationDeliveryException(
                    $"Twilio SMS delivery failed with HTTP {(int)response.StatusCode}.");
            }

            logger.LogInformation(
                "SMS notification {NotificationId} sent to {Phone}",
                notification.Id.Value,
                phone);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(
                ex,
                "SMS notification {NotificationId} could not be sent",
                notification.Id.Value);
            throw new NotificationDeliveryException("Twilio SMS delivery failed.", ex);
        }
    }

    private string? NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var normalized = new string(phone.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (normalized.StartsWith('+'))
            normalized = "+" + new string(normalized.Where(char.IsDigit).ToArray());
        else if (normalized.StartsWith("00"))
            normalized = "+" + normalized[2..];
        else
        {
            var countryCode = _options.DefaultCountryCode.Trim();
            normalized = countryCode + normalized.TrimStart('0');
        }

        var digits = normalized.Count(char.IsDigit);
        return digits >= 8 && digits <= 15 ? normalized : null;
    }
}
