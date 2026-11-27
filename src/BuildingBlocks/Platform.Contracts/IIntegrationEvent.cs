namespace Platform.Contracts;

/// <summary>
/// An event published to other modules, the authorization sync worker and webhooks.
/// Integration events are written to <c>platform.outbox</c> in the same transaction as the
/// aggregate change and are delivered at least once (section 6.4).
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Unique id of this event occurrence; consumers use it for inbox de-duplication.</summary>
    Guid EventId { get; }

    /// <summary>When the change happened (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// An integration event that belongs to one tenant. The outbox stores <see cref="TenantId"/> so the
/// dispatcher can deliver in order per tenant and isolate poison messages per tenant (Z-02).
/// </summary>
public interface ITenantIntegrationEvent : IIntegrationEvent
{
    /// <summary>The tenant the event belongs to.</summary>
    Guid TenantId { get; }
}

/// <summary>Convenience base record for integration events.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.CreateVersion7();

    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
