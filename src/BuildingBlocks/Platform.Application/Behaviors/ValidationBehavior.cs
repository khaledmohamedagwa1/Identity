using FluentValidation;
using Platform.Application.Pipeline;
using Platform.Domain.Results;

namespace Platform.Application.Behaviors;

/// <summary>Runs every FluentValidation validator for the request; on failure the handler is not called (TC-F02-03).</summary>
internal sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
{
    public int Stage => PipelineStage.Validation;

    public async Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return await next().ConfigureAwait(false);
        }

        var details = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);

        return Result<TResponse>.Failure(Error.Validation(
            "platform.validation_failed",
            "One or more fields are invalid.",
            details));
    }
}
