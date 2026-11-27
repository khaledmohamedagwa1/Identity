namespace Platform.Application.Auditing;

/// <summary>
/// Scoped details a handler fills in for the audit entry of the current command
/// (target and before/after values, BR-12). The audit behaviour reads it after success.
/// </summary>
public interface IAuditScope
{
    /// <summary>The changed object, for example <c>org_node:0193…</c>.</summary>
    string? Target { get; set; }

    /// <summary>State before the change (serialized as JSON; no secrets).</summary>
    object? Before { get; set; }

    /// <summary>State after the change (serialized as JSON; no secrets).</summary>
    object? After { get; set; }
}

/// <summary>Default scoped <see cref="IAuditScope"/>.</summary>
public sealed class AuditScope : IAuditScope
{
    /// <inheritdoc />
    public string? Target { get; set; }

    /// <inheritdoc />
    public object? Before { get; set; }

    /// <inheritdoc />
    public object? After { get; set; }
}
