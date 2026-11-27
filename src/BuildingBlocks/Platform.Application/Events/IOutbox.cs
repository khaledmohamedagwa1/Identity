using Platform.Contracts;

namespace Platform.Application.Events;

/// <summary>
/// Queues integration events for the transactional outbox. Infrastructure writes them to
/// <c>platform.outbox</c> in the same transaction as the aggregate change (D3, section 6.4).
/// </summary>
public interface IOutbox
{
    /// <summary>Queues an event; it is persisted on commit and discarded on rollback.</summary>
    void Enqueue(IIntegrationEvent integrationEvent);
}

/// <summary>Scoped buffer behind <see cref="IOutbox"/>; drained by the persistence layer on save.</summary>
public sealed class OutboxBuffer : IOutbox
{
    private readonly List<IIntegrationEvent> _pending = [];

    /// <inheritdoc />
    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        _pending.Add(integrationEvent);
    }

    /// <summary>Events queued and not yet drained.</summary>
    public IReadOnlyList<IIntegrationEvent> Pending => _pending.AsReadOnly();

    /// <summary>Returns and clears the queued events.</summary>
    public IReadOnlyList<IIntegrationEvent> Drain()
    {
        var events = _pending.ToArray();
        _pending.Clear();
        return events;
    }

    /// <summary>Discards queued events (rollback).</summary>
    public void Clear() => _pending.Clear();
}
