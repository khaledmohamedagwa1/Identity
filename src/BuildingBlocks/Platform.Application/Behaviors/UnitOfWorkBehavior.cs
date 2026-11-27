using Platform.Application.Events;
using Platform.Application.Messaging;
using Platform.Application.Persistence;
using Platform.Application.Pipeline;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>
/// Commands only: commits on success; rolls back on a failed result or an exception, so aggregate,
/// outbox and audit rows commit or roll back together (TC-F03-03). Concurrency conflicts become
/// a Conflict error (TC-F03-05).
/// </summary>
internal sealed class UnitOfWorkBehavior<TRequest, TResponse>(IUnitOfWork unitOfWork, OutboxBuffer outbox)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.UnitOfWork;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICommand<TResponse>)
        {
            return await next().ConfigureAwait(false);
        }

        await unitOfWork.BeginAsync(cancellationToken).ConfigureAwait(false);
        Result<TResponse> result;
        try
        {
            result = await next().ConfigureAwait(false);
        }
        catch
        {
            outbox.Clear();
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (result.IsFailure)
        {
            outbox.Clear();
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return result;
        }

        try
        {
            await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            outbox.Clear();
            await unitOfWork.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return Result<TResponse>.Failure(Error.Common.ConcurrencyConflict);
        }

        return result;
    }
}
