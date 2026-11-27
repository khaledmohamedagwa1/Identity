namespace Platform.Domain.Primitives;

/// <summary>Non-generic view of an aggregate root, used by infrastructure to collect events and versions.</summary>
public interface IAggregateRoot
{
    /// <summary>Optimistic concurrency version, incremented on every persisted change.</summary>
    long Version { get; }

    /// <summary>Domain events raised since the last <see cref="ClearEvents"/>, in the order they were raised.</summary>
    IReadOnlyList<IDomainEvent> DomainEvents { get; }

    /// <summary>Returns the pending events in order and clears the list.</summary>
    IReadOnlyList<IDomainEvent> DequeueEvents();

    /// <summary>Removes all pending events.</summary>
    void ClearEvents();

    /// <summary>Called by infrastructure when the aggregate is about to be persisted.</summary>
    void IncrementVersion();
}

/// <summary>
/// The consistency boundary of a DDD aggregate: holds invariants, raises domain events
/// and carries a version for optimistic concurrency.
/// </summary>
/// <typeparam name="TId">The strongly typed id.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Creates an aggregate with the given id.</summary>
    protected AggregateRoot(TId id) : base(id)
    {
    }

    /// <summary>Parameterless constructor for ORM materialization only.</summary>
    protected AggregateRoot()
    {
    }

    /// <inheritdoc />
    public long Version { get; private set; }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Records a domain event to be dispatched before commit.</summary>
    protected void Raise(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <inheritdoc />
    public IReadOnlyList<IDomainEvent> DequeueEvents()
    {
        var events = _domainEvents.ToArray();
        _domainEvents.Clear();
        return events;
    }

    /// <inheritdoc />
    public void ClearEvents() => _domainEvents.Clear();

    /// <inheritdoc />
    void IAggregateRoot.IncrementVersion() => Version++;
}
