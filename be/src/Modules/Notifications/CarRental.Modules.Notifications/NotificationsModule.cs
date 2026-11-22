using CarRental.Modules.Notifications.Data;
using CarRental.Modules.Notifications.Hubs;
using CarRental.Modules.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace CarRental.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema)));

        services.AddScoped<NotificationDeliveryService>();
        services.Configure<NotificationDeliveryOptions>(
            configuration.GetSection(NotificationDeliveryOptions.SectionName));
        services.AddHttpClient("Notifications.Twilio", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<LoggingNotificationDeliveryProvider>();
        services.AddScoped<SmtpEmailDeliveryProvider>();
        services.AddScoped<TwilioSmsDeliveryProvider>();
        services.AddScoped<INotificationDeliveryProvider, ExternalNotificationDeliveryProvider>();
        services.AddScoped<SignalRNotificationDeliveryProvider>();
        services.AddSignalR();

        return services;
    }
}
