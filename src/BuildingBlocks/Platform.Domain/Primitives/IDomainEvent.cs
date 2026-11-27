namespace Platform.Domain.Primitives;

/// <summary>
/// Something that happened inside an aggregate. Domain events are dispatched in-process,
/// before commit and in the same transaction, for same-context side effects (section 6.4).
/// </summary>
public interface IDomainEvent
{
    /// <summary>When the event was raised (UTC).</summary>
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Convenience base record for domain events.</summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
