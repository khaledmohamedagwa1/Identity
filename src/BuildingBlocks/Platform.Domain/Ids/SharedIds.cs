namespace Platform.Domain.Ids;

// Shared-kernel ids: published language used by every bounded context (section 6.2).
// Module-specific ids live in each module's Domain project.

/// <summary>Identifies a tenant (customer organization).</summary>
public readonly record struct TenantId(Guid Value) : IStronglyTypedId<TenantId>
{
    /// <inheritdoc />
    public static TenantId From(Guid value) => new(value);

    /// <summary>Creates a new tenant id.</summary>
    public static TenantId New() => new(Uuid7.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

/// <summary>Identifies a global user account (D6). Appears as <c>sub</c> in tokens.</summary>
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    /// <inheritdoc />
    public static UserId From(Guid value) => new(value);

    /// <summary>Creates a new user id.</summary>
    public static UserId New() => new(Uuid7.NewGuid());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
