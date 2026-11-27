using Platform.Domain.Ids;

namespace Platform.Domain.Primitives;

/// <summary>
/// Marks an entity whose rows belong to exactly one tenant. Infrastructure applies a global
/// query filter on <see cref="TenantId"/> and PostgreSQL row-level security to its table (section 7.3).
/// </summary>
public interface ITenantOwned
{
    /// <summary>The owning tenant. Never changes after creation (BR-03).</summary>
    TenantId TenantId { get; }
}
