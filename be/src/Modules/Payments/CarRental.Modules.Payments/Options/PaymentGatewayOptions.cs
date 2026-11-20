namespace CarRental.Modules.Payments.Options;

internal sealed class PaymentGatewayOptions
{
    public const string SectionName = "Payments";

    public bool UseMockGateway { get; set; }
    public int CheckoutTtlMinutes { get; set; } = 15;
    public string WebhookSecret { get; set; } = string.Empty;
    public VnPayOptions VNPay { get; set; } = new();
    public PayPalOptions PayPal { get; set; } = new();
    public StripeOptions Stripe { get; set; } = new();
    public BankTransferOptions Transfer { get; set; } = new();
}

internal sealed class VnPayOptions
{
    public const string SectionName = "Payments:VNPay";

    public string TmnCode { get; set; } = string.Empty;
    public string HashSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public string Version { get; set; } = "2.1.0";
    public string CurrCode { get; set; } = "VND";
    public string Locale { get; set; } = "vn";
    public string OrderType { get; set; } = "other";
    public string DefaultIpAddress { get; set; } = "127.0.0.1";
    public int CheckoutTtlMinutes { get; set; } = 15;
}

internal sealed class PayPalOptions
{
    public const string SectionName = "Payments:PayPal";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string WebhookId { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 25000m;
    public string BrandName { get; set; } = "CarRental Platform";
    public int CheckoutTtlMinutes { get; set; } = 15;
}

internal sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    public string SecretKey { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty; //Optional key for front end, can be left empty in .env
    public string WebhookSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.stripe.com";
    public string Currency { get; set; } = "vnd";
    public int WebhookToleranceSeconds { get; set; } = 300;
    public int CheckoutTtlMinutes { get; set; } = 30;
}

internal sealed class BankTransferOptions
{
    public const string SectionName = "Payments:Transfer";

    public string BankCode { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string QrTemplate { get; set; } = "compact2";
    public string VietQrBaseUrl { get; set; } = "https://img.vietqr.io/image";
    public string TransferPrefix { get; set; } = "CARRENTAL";
    public int CheckoutTtlMinutes { get; set; } = 15;
}
