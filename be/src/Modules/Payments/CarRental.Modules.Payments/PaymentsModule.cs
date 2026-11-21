using System.Globalization;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Options;
using CarRental.Modules.Payments.Services;
using CarRental.Modules.Payments.Services.Gateways;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CarRental.Modules.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=carrental;Username=postgres;Password=postgres;Include Error Detail=true;";

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, o =>
                o.MigrationsHistoryTable("__EFMigrationsHistory", PaymentsDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.AddValidatorsFromAssembly(typeof(PaymentsModule).Assembly);

        ConfigureGatewayOptions(services, configuration);

        services.AddHttpClient(PaymentGatewayHttpClients.VnPay, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(PaymentGatewayHttpClients.PayPal, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient(PaymentGatewayHttpClients.Stripe, client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<PayPalAccessTokenProvider>();

        services.AddScoped<IPaymentsRepository, PaymentsRepository>();

        services.AddScoped<VnPayPaymentGateway>();
        services.AddScoped<PayPalPaymentGateway>();
        services.AddScoped<StripePaymentGateway>();
        services.AddScoped<BankTransferPaymentGateway>();

        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<VnPayPaymentGateway>());
        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<PayPalPaymentGateway>());
        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<StripePaymentGateway>());
        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<BankTransferPaymentGateway>());
        services.AddScoped<IPayPalOrderCaptureProvider>(sp => sp.GetRequiredService<PayPalPaymentGateway>());

        services.AddScoped<RoutingPaymentGateway>();
        services.AddScoped<MockPaymentGateway>();
        services.AddScoped<IPaymentGateway>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PaymentGatewayOptions>>().Value;
            return opts.UseMockGateway
                ? sp.GetRequiredService<MockPaymentGateway>()
                : sp.GetRequiredService<RoutingPaymentGateway>();
        });

        services.AddScoped<IPaymentsApi, PaymentsApiService>();
        services.AddHostedService<DepositAutoReleaseService>();

        return services;
    }

    private static void ConfigureGatewayOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentGatewayOptions>()
            .BindConfiguration(PaymentGatewayOptions.SectionName)
            .PostConfigure(opts =>
            {
                if (bool.TryParse(configuration["PAYMENTS_USE_MOCK_GATEWAY"], out var flatMock))
                {
                    opts.UseMockGateway = flatMock;
                }
            });

        services.AddOptions<VnPayOptions>()
            .BindConfiguration(VnPayOptions.SectionName)
            .PostConfigure(opts =>
            {
                var defaults = new VnPayOptions();
                opts.TmnCode = PreferStructuredSecret(opts.TmnCode, configuration["VNPAY_TMN_CODE"]);
                opts.HashSecret = PreferStructuredSecret(opts.HashSecret, configuration["VNPAY_HASH_SECRET"]);
                opts.BaseUrl = PreferFlatWhenDefault(opts.BaseUrl, defaults.BaseUrl, configuration["VNPAY_BASE_URL"]);
                opts.ApiUrl = PreferFlatWhenDefault(opts.ApiUrl, defaults.ApiUrl, configuration["VNPAY_API_URL"]);
            });

        services.AddOptions<PayPalOptions>()
            .BindConfiguration(PayPalOptions.SectionName)
            .PostConfigure(opts =>
            {
                var defaults = new PayPalOptions();
                opts.ClientId = PreferStructuredSecret(opts.ClientId, configuration["PAYPAL_CLIENT_ID"]);
                opts.ClientSecret = PreferStructuredSecret(opts.ClientSecret, configuration["PAYPAL_CLIENT_SECRET"]);
                opts.WebhookId = PreferStructuredSecret(opts.WebhookId, configuration["PAYPAL_WEBHOOK_ID"]);
                opts.BaseUrl = PreferFlatWhenDefault(opts.BaseUrl, defaults.BaseUrl, configuration["PAYPAL_BASE_URL"]);
                opts.Currency = PreferFlatWhenDefault(opts.Currency, defaults.Currency, configuration["PAYPAL_CURRENCY"]);

                if (opts.ExchangeRate == defaults.ExchangeRate
                    && decimal.TryParse(
                        configuration["PAYPAL_EXCHANGE_RATE"],
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out var rate)
                    && rate > 0m)
                {
                    opts.ExchangeRate = rate;
                }
            });

        services.AddOptions<StripeOptions>()
            .BindConfiguration(StripeOptions.SectionName)
            .PostConfigure(opts =>
            {
                var defaults = new StripeOptions();
                opts.SecretKey = PreferStructuredSecret(opts.SecretKey, configuration["STRIPE_SECRET_KEY"]);
                opts.PublishableKey = PreferStructuredSecret(opts.PublishableKey, configuration["STRIPE_PUBLISHABLE_KEY"]);
                opts.WebhookSecret = PreferStructuredSecret(opts.WebhookSecret, configuration["STRIPE_WEBHOOK_SECRET"]);
                opts.ApiBaseUrl = PreferFlatWhenDefault(opts.ApiBaseUrl, defaults.ApiBaseUrl, configuration["STRIPE_API_BASE_URL"]);
                opts.Currency = PreferFlatWhenDefault(opts.Currency, defaults.Currency, configuration["STRIPE_CURRENCY"]);
            });

        services.AddOptions<BankTransferOptions>()
            .BindConfiguration(BankTransferOptions.SectionName)
            .PostConfigure(opts =>
            {
                opts.BankCode = PreferStructuredSecret(opts.BankCode, configuration["TRANSFER_BANK_CODE"]);
                opts.AccountNumber = PreferStructuredSecret(opts.AccountNumber, configuration["TRANSFER_ACCOUNT_NUMBER"]);
                opts.AccountName = PreferStructuredSecret(opts.AccountName, configuration["TRANSFER_ACCOUNT_NAME"]);
                opts.WebhookSecret = PreferStructuredSecret(opts.WebhookSecret, configuration["TRANSFER_WEBHOOK_SECRET"]);
            });
    }

    private static string PreferStructuredSecret(string structuredValue, string? flatValue) =>
        !string.IsNullOrWhiteSpace(structuredValue)
            ? structuredValue.Trim()
            : (flatValue?.Trim() ?? string.Empty);

    private static string PreferFlatWhenDefault(string currentValue, string defaultValue, string? flatValue)
    {
        if (!string.IsNullOrWhiteSpace(flatValue)
            && (string.IsNullOrWhiteSpace(currentValue) || string.Equals(currentValue, defaultValue, StringComparison.Ordinal)))
        {
            return flatValue.Trim();
        }

        return string.IsNullOrWhiteSpace(currentValue) ? defaultValue : currentValue.Trim();
    }
}
