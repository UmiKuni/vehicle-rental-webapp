namespace CarRental.Api.Infrastructure.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public RateLimitPolicyOptions AuthLogin { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitPolicyOptions AuthRegister { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };

    public RateLimitPolicyOptions AuthRefresh { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    public RateLimitPolicyOptions BookingsCreate { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };

    public RateLimitPolicyOptions PaymentsCheckout { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;
}
