using Platform.Domain.Results;

namespace Platform.Application.Messaging;

/// <summary>
/// Sends commands and queries through the fixed pipeline to their single handler.
/// Our own small dispatcher (decision D5: no MediatR 13+).
/// </summary>
public interface IDispatcher
{
    /// <summary>Sends a command or query.</summary>
    Task<Result<TResponse>> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
