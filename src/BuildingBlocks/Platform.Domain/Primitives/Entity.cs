namespace Platform.Domain.Primitives;

/// <summary>An object with identity. Two entities are equal when their type and id are equal.</summary>
/// <typeparam name="TId">The strongly typed id.</typeparam>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    /// <summary>Creates an entity with the given id.</summary>
    protected Entity(TId id) => Id = id;

    /// <summary>Parameterless constructor for ORM materialization only.</summary>
    protected Entity() => Id = default!;

    /// <summary>The identity of the entity.</summary>
    public TId Id { get; protected init; }

    /// <inheritdoc />
    public bool Equals(Entity<TId>? other) =>
        other is not null && (ReferenceEquals(this, other) || (other.GetType() == GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id)));

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Entity<TId> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    /// <summary>Equality by type and id.</summary>
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);

    /// <summary>Inequality by type and id.</summary>
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
