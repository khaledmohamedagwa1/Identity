using FluentValidation;
using Platform.Application.Messaging;
using Platform.Domain.Ids;
using Platform.Domain.Primitives;
using Platform.Domain.Results;

namespace Platform.BuildingBlocks.Tests;

public sealed class Money(decimal amount, string currency) : ValueObject
{
    public decimal Amount { get; } = amount;
    public string Currency { get; } = currency;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency.ToUpperInvariant();
    }
}

public readonly record struct SampleId(Guid Value) : IStronglyTypedId<SampleId>
{
    public static SampleId From(Guid value) => new(value);
    public static SampleId New() => new(Uuid7.NewGuid());
}

public sealed record SampleCreated(SampleId Id) : DomainEvent;
public sealed record SampleRenamed(SampleId Id, string Name) : DomainEvent;

public sealed class SampleAggregate : AggregateRoot<SampleId>
{
    public SampleAggregate(SampleId id, string name) : base(id)
    {
        Name = name;
        Raise(new SampleCreated(id));
    }

    public string Name { get; private set; }

    public void Rename(string name)
    {
        Name = name;
        Raise(new SampleRenamed(Id, name));
    }
}

public sealed record CreateSample(string Name, string? IdempotencyKey = null) : ICommand<Guid>, IIdempotentCommand, IAuditableCommand
{
    public string AuditAction => "sample.create";
}

public sealed class CreateSampleValidator : AbstractValidator<CreateSample>
{
    public CreateSampleValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(10);
}

public sealed class CreateSampleHandler : ICommandHandler<CreateSample, Guid>
{
    public int Calls { get; private set; }
    public Func<CreateSample, Result<Guid>>? Behavior { get; set; }

    public Task<Result<Guid>> Handle(CreateSample request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Behavior?.Invoke(request) ?? Result.Success(Uuid7.NewGuid()));
    }
}

public sealed record GetSample(Guid Id) : IQuery<string>;

public sealed class GetSampleHandler : IQueryHandler<GetSample, string>
{
    public Task<Result<string>> Handle(GetSample request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<string>>(Error.NotFound("sample.not_found", "Sample not found."));
}

public sealed record PlatformPing : ICommand, ITenantAgnosticRequest;

public sealed class PlatformPingHandler : ICommandHandler<PlatformPing>
{
    public Task<Result<Unit>> Handle(PlatformPing request, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(Unit.Value));
}
