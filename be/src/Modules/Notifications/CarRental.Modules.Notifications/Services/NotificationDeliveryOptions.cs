namespace CarRental.Modules.Notifications.Services;

public sealed class NotificationDeliveryOptions
{
    public const string SectionName = "Notifications";

    public SmtpDeliveryOptions Smtp { get; set; } = new();
    public TwilioDeliveryOptions Twilio { get; set; } = new();
}

public sealed class SmtpDeliveryOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "CarRental";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class TwilioDeliveryOptions
{
    public string AccountSid { get; set; } = string.Empty;
    public string AuthToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
    public string MessagingServiceSid { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.twilio.com";
    public string DefaultCountryCode { get; set; } = "+84";
}
