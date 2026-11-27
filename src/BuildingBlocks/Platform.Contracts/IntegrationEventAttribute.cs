namespace Platform.Contracts;

/// <summary>
/// Declares the stable wire name and schema version of an integration event.
/// The name never changes once published; breaking changes bump <see cref="Version"/>
/// through a contract-change PR (section 8.3).
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class IntegrationEventAttribute(string name, int version) : Attribute
{
    /// <summary>Stable wire name, for example <c>access.role_assigned</c>.</summary>
    public string Name { get; } = name;

    /// <summary>Schema version, starting at 1.</summary>
    public int Version { get; } = version;

    /// <summary>The value stored in the outbox <c>type</c> column, for example <c>access.role_assigned.v1</c>.</summary>
    public string TypeKey => $"{Name}.v{Version}";
}
