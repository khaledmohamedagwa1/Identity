using System.Text.Json;
using Platform.Application.Idempotency;
using Platform.Application.Messaging;
using Platform.Application.Pipeline;
using Platform.Application.Tenancy;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>
/// Returns the stored result when an <see cref="IIdempotentCommand"/> repeats its key; the handler
/// runs once (TC-F02-04). Only successful results are stored, so failed attempts can be retried.
/// Keys are scoped by tenant and command type, so one tenant can never read another's result.
/// </summary>
internal sealed class IdempotencyBehavior<TRequest, TResponse>(IIdempotencyStore store, ITenantContext tenant)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.Idempotency;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand { IdempotencyKey: { Length: > 0 } key })
        {
            return await next().ConfigureAwait(false);
        }

        var scopedKey = $"{tenant.TenantId?.Value.ToString() ?? "platform"}:{typeof(TRequest).FullName}:{key}";
        var stored = await store.GetAsync(scopedKey, cancellationToken).ConfigureAwait(false);
        if (stored is not null)
        {
            if (!string.Equals(stored.ResponseType, typeof(TResponse).FullName, StringComparison.Ordinal))
            {
                return Result<TResponse>.Failure(Error.Conflict(
                    "platform.idempotency_key_reused", "The idempotency key was already used for a different operation."));
            }

            return Result<TResponse>.Success(JsonSerializer.Deserialize<TResponse>(stored.ValueJson, PlatformJson.Options)!);
        }

        var result = await next().ConfigureAwait(false);
        if (result.IsSuccess)
        {
            var json = JsonSerializer.Serialize(result.Value, PlatformJson.Options);
            await store.SaveAsync(scopedKey, new StoredResponse(typeof(TResponse).FullName!, json), cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
