namespace Platform.Domain.Primitives;

/// <summary>
/// An immutable object defined only by its values. Prefer C# records for simple value objects;
/// derive from this class when equality needs custom components (for example case-insensitive strings).
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>The values that define equality, in a stable order.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc />
    public bool Equals(ValueObject? other) =>
        other is not null && other.GetType() == GetType() && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ValueObject other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(GetType());
        foreach (var component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <summary>Equality by components.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) => Equals(left, right);

    /// <summary>Inequality by components.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) => !Equals(left, right);
}
