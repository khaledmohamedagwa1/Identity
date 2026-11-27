namespace Platform.Domain.Ids;

/// <summary>Non-generic view of a strongly typed id wrapping a UUID.</summary>
public interface IStronglyTypedId
{
    /// <summary>The underlying UUID value.</summary>
    Guid Value { get; }
}

/// <summary>
/// A strongly typed id. Implement it on a <c>readonly record struct</c> so ids of different
/// aggregates cannot be mixed up. EF Core and JSON converters work for every implementation.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IStronglyTypedId<TSelf> : IStronglyTypedId
    where TSelf : struct, IStronglyTypedId<TSelf>
{
    /// <summary>Wraps an existing UUID.</summary>
    static abstract TSelf From(Guid value);

    /// <summary>Creates a new id with a monotonic UUIDv7.</summary>
    static virtual TSelf New() => TSelf.From(Uuid7.NewGuid());
}
