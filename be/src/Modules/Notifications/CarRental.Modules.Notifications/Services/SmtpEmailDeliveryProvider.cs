using System.Net;
using System.Net.Mail;
using CarRental.Modules.Notifications.Contracts.Enums;
using CarRental.Modules.Notifications.Entities;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Notifications.Services;

public sealed class SmtpEmailDeliveryProvider(
    IOptions<NotificationDeliveryOptions> options,
    ILogger<SmtpEmailDeliveryProvider> logger)
{
    private readonly SmtpDeliveryOptions _options = options.Value.Smtp;

    public async Task DeliverAsync(
        Notification notification,
        UserContactDto? contact,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new NotificationDeliveryException("SMTP email delivery is not configured.");

        if (contact is null ||
            !MailAddress.TryCreate(contact.Email, out var toAddress))
            throw new NotificationDeliveryException("The notification recipient has no valid email address.");

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = $"CarRental notification: {notification.Type}",
            Body = notification.Content,
            IsBodyHtml = false
        };
        message.To.Add(toAddress);

        using var smtp = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Clamp(_options.TimeoutSeconds, 5, 120) * 1000
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            smtp.UseDefaultCredentials = false;
            smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            await smtp.SendMailAsync(message, ct);
            logger.LogInformation(
                "Email notification {NotificationId} sent to {Email}",
                notification.Id.Value,
                contact.Email);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is SmtpException or IOException)
        {
            logger.LogWarning(
                ex,
                "Email notification {NotificationId} could not be sent",
                notification.Id.Value);
            throw new NotificationDeliveryException("SMTP email delivery failed.", ex);
        }
    }
}
