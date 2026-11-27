using Microsoft.AspNetCore.Http;

namespace Platform.Api;

/// <summary>Helpers for module endpoints.</summary>
public static class HttpContextExtensions
{
    /// <summary>Header carrying the client's idempotency key for retry-safe commands.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>Header returned by AdminApi writes with the outbox revision for read-your-writes (section 5.5).</summary>
    public const string AuthzRevisionHeader = "X-Authz-Revision";

    /// <summary>Reads the <c>Idempotency-Key</c> header (max 200 characters), or <c>null</c>.</summary>
    public static string? GetIdempotencyKey(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var value = context.Request.Headers[IdempotencyKeyHeader].ToString();
        return value.Length is > 0 and <= 200 ? value : null;
    }
}
