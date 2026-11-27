using Platform.Domain.Ids;

namespace Platform.Application.Tenancy;

/// <summary>
/// The tenant and user the current request or job acts for. In APIs it comes only from the
/// token's <c>tid</c> and <c>sub</c> claims, never from headers (section 7.3). Background jobs set it
/// explicitly through <see cref="ITenantContextSetter"/>.
/// </summary>
public interface ITenantContext
{
    /// <summary>The active tenant, or <c>null</c> outside any tenant.</summary>
    TenantId? TenantId { get; }

    /// <summary>The acting user (token <c>sub</c>), or <c>null</c> for machine and anonymous calls.</summary>
    UserId? UserId { get; }

    /// <summary>The support user when impersonating (token <c>act.sub</c>), otherwise <c>null</c>.</summary>
    UserId? ImpersonatorId { get; }

    /// <summary>True when <see cref="TenantId"/> is set.</summary>
    bool HasTenant => TenantId is not null;
}

/// <summary>Sets the tenant context. Used by the API middleware and by background jobs only.</summary>
public interface ITenantContextSetter
{
    /// <summary>Sets the context for the current scope.</summary>
    void Set(TenantId? tenantId, UserId? userId = null, UserId? impersonatorId = null);
}

/// <summary>Scoped default implementation of <see cref="ITenantContext"/>.</summary>
public sealed class TenantContext : ITenantContext, ITenantContextSetter
{
    /// <inheritdoc />
    public TenantId? TenantId { get; private set; }

    /// <inheritdoc />
    public UserId? UserId { get; private set; }

    /// <inheritdoc />
    public UserId? ImpersonatorId { get; private set; }

    /// <inheritdoc />
    public void Set(TenantId? tenantId, UserId? userId = null, UserId? impersonatorId = null)
    {
        TenantId = tenantId;
        UserId = userId;
        ImpersonatorId = impersonatorId;
    }
}
