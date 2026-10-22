using CarRental.BuildingBlocks.Domain;

namespace CarRental.Modules.Support.Entities;

public class TicketMessage : Entity<Guid>
{
    public TicketId SupportTicketId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? AttachmentKey { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private TicketMessage() { }

    public TicketMessage(
        Guid id,
        TicketId supportTicketId,
        Guid senderId,
        string content,
        string? attachmentKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        SupportTicketId = supportTicketId;
        SenderId = senderId;
        Content = content.Trim();
        AttachmentKey = string.IsNullOrWhiteSpace(attachmentKey) ? null : attachmentKey.Trim();
        CreatedAt = createdAt;
    }
}
