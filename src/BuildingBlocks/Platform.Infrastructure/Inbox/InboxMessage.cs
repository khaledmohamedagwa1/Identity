namespace Platform.Infrastructure.Inbox;

/// <summary>A row of <c>platform.inbox</c>: one per (event, consumer) that was processed.</summary>
public sealed class InboxMessage
{
    private InboxMessage()
    {
        Consumer = string.Empty;
    }

    /// <summary>Creates a record.</summary>
    public InboxMessage(Guid eventId, string consumer, DateTimeOffset processedAt)
    {
        EventId = eventId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }

    /// <summary>The event occurrence id.</summary>
    public Guid EventId { get; private set; }

    /// <summary>Consumer name, for example <c>authorization.sync</c>.</summary>
    public string Consumer { get; private set; }

    /// <summary>When it was processed.</summary>
    public DateTimeOffset ProcessedAt { get; private set; }
}
