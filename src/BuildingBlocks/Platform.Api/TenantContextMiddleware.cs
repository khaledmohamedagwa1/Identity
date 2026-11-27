using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Platform.Application.Tenancy;
using Platform.Domain.Ids;

namespace Platform.Api;

/// <summary>
/// Sets <see cref="ITenantContext"/> from the authenticated principal: <c>tid</c> (tenant), <c>sub</c> (user)
/// and <c>act.sub</c> (impersonator). The tenant is taken only from the token, never from headers or
/// query strings (section 7.3). Unauthenticated requests get an empty context, so tenant-scoped
/// commands fail closed in the tenant guard.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    /// <summary>Claim carrying the active tenant.</summary>
    public const string TenantClaim = "tid";

    /// <summary>Claim carrying the global user id.</summary>
    public const string SubjectClaim = "sub";

    /// <summary>Claim carrying the actor during impersonation or token exchange (RFC 8693).</summary>
    public const string ActorClaim = "act";

    /// <summary>Runs the middleware.</summary>
    public Task InvokeAsync(HttpContext context, ITenantContextSetter setter)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(setter);
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            setter.Set(
                ReadGuid(user, TenantClaim) is { } tid ? TenantId.From(tid) : null,
                ReadGuid(user, SubjectClaim, ClaimTypes.NameIdentifier) is { } sub ? UserId.From(sub) : null,
                ReadActor(user) is { } act ? UserId.From(act) : null);
        }

        return next(context);
    }

    private static Guid? ReadGuid(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            if (Guid.TryParse(user.FindFirst(type)?.Value, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static Guid? ReadActor(ClaimsPrincipal user)
    {
        var act = user.FindFirst(ActorClaim)?.Value;
        if (string.IsNullOrEmpty(act))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(act);
            return doc.RootElement.TryGetProperty("sub", out var sub) && Guid.TryParse(sub.GetString(), out var id) ? id : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Pipeline registration helpers.</summary>
public static class TenantContextMiddlewareExtensions
{
    /// <summary>Adds <see cref="TenantContextMiddleware"/>; call after <c>UseAuthentication</c>.</summary>
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) =>
        app.UseMiddleware<TenantContextMiddleware>();
}
