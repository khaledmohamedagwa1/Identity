using System.Diagnostics;
using System.Text.Json;
using Platform.Application.Auditing;
using Platform.Application.Events;
using Platform.Application.Messaging;
using Platform.Application.Pipeline;
using Platform.Application.Tenancy;
using Platform.Contracts.Auditing;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>
/// After a successful <see cref="IAuditableCommand"/>, queues an <see cref="AuditRecordedIntegrationEvent"/>
/// in the outbox. It runs inside the unit of work, so the audit event commits atomically with the change (BR-12).
/// </summary>
internal sealed class AuditBehavior<TRequest, TResponse>(IOutbox outbox, ITenantContext tenant, IAuditScope scope, TimeProvider clock)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.Audit;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var result = await next().ConfigureAwait(false);
        if (result.IsFailure || request is not IAuditableCommand auditable)
        {
            return result;
        }

        outbox.Enqueue(new AuditRecordedIntegrationEvent(
            TenantId: tenant.TenantId?.Value,
            Action: auditable.AuditAction,
            ActorId: tenant.UserId?.Value,
            ImpersonatorId: tenant.ImpersonatorId?.Value,
            Target: scope.Target,
            BeforeJson: scope.Before is null ? null : JsonSerializer.Serialize(scope.Before, PlatformJson.Options),
            AfterJson: scope.After is null ? null : JsonSerializer.Serialize(scope.After, PlatformJson.Options),
            CorrelationId: Activity.Current?.TraceId.ToString())
        {
            OccurredAt = clock.GetUtcNow(),
        });

        return result;
    }
}
