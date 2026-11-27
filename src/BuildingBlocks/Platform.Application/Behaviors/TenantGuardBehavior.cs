using Platform.Application.Messaging;
using Platform.Application.Pipeline;
using Platform.Application.Tenancy;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>
/// Fails closed: every request needs a tenant context unless it implements
/// <see cref="ITenantAgnosticRequest"/> (platform-level operations).
/// </summary>
internal sealed class TenantGuardBehavior<TRequest, TResponse>(ITenantContext tenant)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.TenantGuard;

    public Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is ITenantAgnosticRequest || tenant.TenantId is not null)
        {
            return next();
        }

        return Task.FromResult(Result<TResponse>.Failure(Error.Common.TenantRequired));
    }
}
