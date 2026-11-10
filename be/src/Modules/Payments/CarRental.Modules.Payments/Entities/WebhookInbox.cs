using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Payments.Entities;

public class WebhookInbox : Entity<string>
{
    public DateTimeOffset ProcessedAt { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;

    private WebhookInbox() { }

    public WebhookInbox(string id, DateTimeOffset processedAt, string payloadJson)
    {
        Id = id;
        ProcessedAt = processedAt;
        PayloadJson = payloadJson;
    }
}
