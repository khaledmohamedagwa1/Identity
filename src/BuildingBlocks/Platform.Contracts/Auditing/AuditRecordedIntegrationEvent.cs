namespace Platform.Contracts.Auditing;

/// <summary>
/// An explicit audit entry produced by the audit pipeline behaviour for a privileged command (BR-12).
/// Consumed by the Audit module (U-01). Never contains secrets, tokens or passwords.
/// </summary>
/// <param name="TenantId">Tenant the action happened in; null for platform-level actions.</param>
/// <param name="Action">Stable action name, for example <c>access.role.assign</c>.</param>
/// <param name="ActorId">User who acted (token <c>sub</c>), if any.</param>
/// <param name="ImpersonatorId">Support user when impersonating, if any.</param>
/// <param name="Target">Changed object, for example <c>org_node:…</c>.</param>
/// <param name="BeforeJson">State before the change.</param>
/// <param name="AfterJson">State after the change.</param>
/// <param name="CorrelationId">W3C trace id of the request.</param>
[IntegrationEvent("platform.audit_recorded", 1)]
public sealed record AuditRecordedIntegrationEvent(
    Guid? TenantId,
    string Action,
    Guid? ActorId,
    Guid? ImpersonatorId,
    string? Target,
    string? BeforeJson,
    string? AfterJson,
    string? CorrelationId) : IntegrationEvent;
