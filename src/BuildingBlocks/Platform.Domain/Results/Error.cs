namespace Platform.Domain.Results;

/// <summary>Category of an <see cref="Error"/>; drives the HTTP status in Problem Details (ADR-0010).</summary>
public enum ErrorType
{
    /// <summary>Input or an invariant was violated (400).</summary>
    Validation = 1,

    /// <summary>The target does not exist or is not visible to the caller (404).</summary>
    NotFound = 2,

    /// <summary>The change conflicts with current state: duplicates, concurrency, illegal transition (409).</summary>
    Conflict = 3,

    /// <summary>The caller may not perform the action (403).</summary>
    Forbidden = 4,
}

/// <summary>
/// A typed, expected failure. Codes are stable, dot-separated and lowercase, for example
/// <c>org.node.cycle</c>; clients and tests rely on them. Messages are for humans and may change.
/// </summary>
/// <param name="Code">Stable machine-readable code.</param>
/// <param name="Message">Human-readable description (no personal data).</param>
/// <param name="Type">Category.</param>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Field-level details for validation errors: field name to messages.</summary>
    public IReadOnlyDictionary<string, string[]> Details { get; init; } = EmptyDetails;

    private static readonly IReadOnlyDictionary<string, string[]> EmptyDetails = new Dictionary<string, string[]>();

    /// <summary>Creates a validation error.</summary>
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, message, ErrorType.Validation) { Details = details ?? EmptyDetails };

    /// <summary>Creates a not-found error.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates a conflict error.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Creates a forbidden error.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Well-known platform errors.</summary>
    public static class Common
    {
        /// <summary>Optimistic concurrency conflict (TC-F03-05).</summary>
        public static readonly Error ConcurrencyConflict =
            Conflict("platform.concurrency_conflict", "The resource was changed by someone else. Reload and try again.");

        /// <summary>A tenant-scoped request ran without a tenant context.</summary>
        public static readonly Error TenantRequired =
            Forbidden("platform.tenant_required", "This operation requires a tenant context.");
    }
}
