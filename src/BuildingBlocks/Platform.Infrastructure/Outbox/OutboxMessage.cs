using System.Text.Json;
using Platform.Application;
using Platform.Contracts;
using Platform.Contracts.Auditing;

namespace Platform.Infrastructure.Outbox;

/// <summary>
/// A row of <c>platform.outbox</c>. <see cref="Id"/> is a <c>bigserial</c> and doubles as the
/// authorization revision returned in <c>X-Authz-Revision</c> (section 5.5).
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
        Type = string.Empty;
        Payload = string.Empty;
    }

    /// <summary>Sequence number (revision).</summary>
    public long Id { get; private set; }

    /// <summary>Event occurrence id; consumers de-duplicate on it (inbox).</summary>
    public Guid EventId { get; private set; }

    /// <summary>Owning tenant, or null for platform-level events.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Wire type key, for example <c>access.role_assigned.v1</c>.</summary>
    public string Type { get; private set; }

    /// <summary>JSON payload (<c>jsonb</c>).</summary>
    public string Payload { get; private set; }

    /// <summary>When the change happened.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>When the dispatcher delivered the message; null while pending.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Creates an outbox row for <paramref name="integrationEvent"/>.</summary>
    public static OutboxMessage From(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        var type = integrationEvent.GetType();
        return new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            TenantId = TenantOf(integrationEvent),
            Type = IntegrationEventMetadata.For(type).TypeKey,
            Payload = JsonSerializer.Serialize(integrationEvent, type, PlatformJson.Options),
            OccurredAt = integrationEvent.OccurredAt,
        };
    }

    /// <summary>Marks the row as delivered.</summary>
    public void MarkProcessed(DateTimeOffset at) => ProcessedAt = at;

    private static Guid? TenantOf(IIntegrationEvent e) => e switch
    {
        ITenantIntegrationEvent t => t.TenantId,
        AuditRecordedIntegrationEvent a => a.TenantId,
        _ => null,
    };
}
