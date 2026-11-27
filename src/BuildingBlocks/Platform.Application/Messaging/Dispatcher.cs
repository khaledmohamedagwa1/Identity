using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Pipeline;
using Platform.Domain.Results;

namespace Platform.Application.Messaging;

/// <summary>
/// The in-house dispatcher (~100 lines): resolves the single handler for a request, wraps it in
/// the behaviours ordered by <see cref="IPipelineBehavior{TRequest,TResponse}.Stage"/> and runs it.
/// </summary>
internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Wrappers = new();

    public Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(
            request.GetType(),
            static requestType => Activator.CreateInstance(
                typeof(RequestWrapper<,>).MakeGenericType(requestType, typeof(TResponse)))!);
        return wrapper.Handle(request, services, cancellationToken);
    }
}

internal abstract class RequestWrapper<TResponse>
{
    public abstract Task<Result<TResponse>> Handle(object request, IServiceProvider services, CancellationToken cancellationToken);
}

internal sealed class RequestWrapper<TRequest, TResponse> : RequestWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public override Task<Result<TResponse>> Handle(object request, IServiceProvider services, CancellationToken cancellationToken)
    {
        var typed = (TRequest)request;
        var handler = services.GetService<IRequestHandler<TRequest, TResponse>>()
            ?? throw new InvalidOperationException($"No handler registered for '{typeof(TRequest).FullName}'.");

        // OrderBy is stable: equal stages keep registration order.
        var behaviors = services.GetServices<IPipelineBehavior<TRequest, TResponse>>()
            .OrderBy(b => b.Stage)
            .ToArray();

        RequestHandlerDelegate<TResponse> next = () => handler.Handle(typed, cancellationToken);
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var inner = next;
            next = () => behavior.Handle(typed, inner, cancellationToken);
        }

        return next();
    }
}
