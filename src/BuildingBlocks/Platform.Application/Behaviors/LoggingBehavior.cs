using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Platform.Application.Pipeline;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>Logs request name, outcome, error code and duration. Never logs request payloads (they may hold personal data).</summary>
internal sealed partial class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.Logging;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = await next().ConfigureAwait(false);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (result.IsSuccess)
            {
                LogSuccess(logger, name, elapsed);
            }
            else
            {
                LogFailure(logger, name, result.Error.Code, result.Error.Type, elapsed);
            }

            return result;
        }
        catch (Exception ex)
        {
            LogException(logger, ex, name, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs:0.0} ms")]
    private static partial void LogSuccess(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed with {ErrorCode} ({ErrorType}) in {ElapsedMs:0.0} ms")]
    private static partial void LogFailure(ILogger logger, string requestName, string errorCode, ErrorType errorType, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "{RequestName} threw after {ElapsedMs:0.0} ms")]
    private static partial void LogException(ILogger logger, Exception exception, string requestName, double elapsedMs);
}
