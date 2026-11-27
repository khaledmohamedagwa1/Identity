using Platform.Domain.Results;

namespace Platform.Application.Pipeline;

/// <summary>Continues the pipeline with the next behaviour or the handler.</summary>
public delegate Task<Result<TResponse>> RequestHandlerDelegate<TResponse>();

/// <summary>
/// A cross-cutting step around request handling. Behaviours run ordered by <see cref="Stage"/>
/// (lowest first, outermost); ties keep registration order. See <see cref="PipelineStage"/>.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>Position in the pipeline.</summary>
    int Stage { get; }

    /// <summary>Runs the behaviour; call <paramref name="next"/> to continue.</summary>
    Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

/// <summary>
/// The fixed pipeline order (F-02). Built-in behaviours use these stages; module behaviours
/// default to <see cref="Module"/> and therefore run innermost, right before the handler.
/// </summary>
public static class PipelineStage
{
    /// <summary>1. Structured logging and timing (outermost; sees every outcome).</summary>
    public const int Logging = 100;

    /// <summary>2. FluentValidation; invalid input never reaches later stages or the handler.</summary>
    public const int Validation = 200;

    /// <summary>3. Tenant guard: tenant-scoped requests require a tenant context.</summary>
    public const int TenantGuard = 300;

    /// <summary>4. Idempotency: a repeated key returns the stored result without running the handler.</summary>
    public const int Idempotency = 400;

    /// <summary>5. Unit of work: commits on success, rolls back on failure or exception (commands only).</summary>
    public const int UnitOfWork = 500;

    /// <summary>6. Audit: records successful auditable commands inside the same transaction.</summary>
    public const int Audit = 600;

    /// <summary>7. Module-specific behaviours.</summary>
    public const int Module = 1000;
}
