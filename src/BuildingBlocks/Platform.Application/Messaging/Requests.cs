using Platform.Domain.Results;

namespace Platform.Application.Messaging;

/// <summary>Base marker for anything the dispatcher handles. Handlers always return <see cref="Result{T}"/>.</summary>
/// <typeparam name="TResponse">The success value type.</typeparam>
#pragma warning disable CA1040 // Marker interfaces are intended here: they drive pipeline behaviour selection.
public interface IRequest<TResponse>;

/// <summary>A request that changes state. Runs inside a unit of work.</summary>
public interface ICommand<TResponse> : IRequest<TResponse>;

/// <summary>A command without a return value.</summary>
public interface ICommand : ICommand<Unit>;

/// <summary>A request that reads state. Never runs in a unit of work and must not change state.</summary>
public interface IQuery<TResponse> : IRequest<TResponse>;

/// <summary>
/// Opt-out from the tenant guard: platform-level requests (operator console, tenant creation)
/// that legitimately run without a tenant. Every other request requires <c>ITenantContext.TenantId</c>.
/// </summary>
public interface ITenantAgnosticRequest;
#pragma warning restore CA1040

/// <summary>A command that can be retried safely by sending the same idempotency key.</summary>
public interface IIdempotentCommand
{
    /// <summary>Client-supplied key (for example the <c>Idempotency-Key</c> header). Null disables de-duplication.</summary>
    string? IdempotencyKey { get; }
}

/// <summary>A command that must leave an audit entry on success (BR-12).</summary>
public interface IAuditableCommand
{
    /// <summary>Stable action name, for example <c>access.role.assign</c>.</summary>
    string AuditAction { get; }
}
