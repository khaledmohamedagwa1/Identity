using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Domain.Results;

namespace Platform.Api;

/// <summary>
/// Maps <see cref="Result"/> failures to RFC 9457 Problem Details (ADR-0010). Every problem carries
/// the stable error <c>code</c> and the request <c>traceId</c> as extension members.
/// </summary>
public static class ProblemDetailsMapping
{
    /// <summary>Prefix of the problem <c>type</c> URI; the error code is appended.</summary>
    public const string TypePrefix = "urn:platform:error:";

    /// <summary>HTTP status for an error type.</summary>
    public static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>Builds the Problem Details body for <paramref name="error"/>.</summary>
    public static ProblemDetails ToProblemDetails(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var status = StatusCodeFor(error.Type);
        ProblemDetails problem = error.Type == ErrorType.Validation && error.Details.Count > 0
            ? new ValidationProblemDetails(error.Details.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal))
            : new ProblemDetails();
        problem.Status = status;
        problem.Type = TypePrefix + error.Code;
        problem.Title = error.Type.ToString();
        problem.Detail = error.Message;
        problem.Extensions["code"] = error.Code;
        if (Activity.Current is { } activity)
        {
            problem.Extensions["traceId"] = activity.TraceId.ToString();
        }

        return problem;
    }

    /// <summary>Converts an error to an <see cref="IResult"/> with <c>application/problem+json</c>.</summary>
    public static IResult ToProblem(this Error error)
    {
        var problem = ToProblemDetails(error);
        return Results.Problem(problem);
    }

    /// <summary>Returns <paramref name="onSuccess"/> for a success, otherwise Problem Details.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();
    }

    /// <summary>Returns 200 with the value for a success, otherwise Problem Details.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result) => result.ToHttpResult(static v => Results.Ok(v));

    /// <summary>Returns 204 for a success, otherwise Problem Details.</summary>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
    }
}
