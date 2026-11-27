using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Platform.Api;
using Platform.Application;
using Platform.Application.Auditing;
using Platform.Application.Events;
using Platform.Application.Messaging;
using Platform.Application.Persistence;
using Platform.Application.Pipeline;
using Platform.Application.Tenancy;
using Platform.Contracts.Auditing;
using Platform.Domain.Ids;
using Platform.Domain.Results;

namespace Platform.BuildingBlocks.Tests;

public sealed class PipelineTests
{
    private static (ServiceProvider Provider, CreateSampleHandler Handler, IUnitOfWork Uow) Build(TenantId? tenant = null, bool withTenant = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var uow = Substitute.For<IUnitOfWork>();
        services.AddScoped(_ => uow);
        services.AddModuleHandlers(typeof(PipelineTests).Assembly);
        var handler = new CreateSampleHandler();
        services.AddScoped<IRequestHandler<CreateSample, Guid>>(_ => handler);
        var provider = services.BuildServiceProvider();
        if (withTenant)
        {
            provider.GetRequiredService<ITenantContextSetter>().Set(tenant ?? TenantId.New(), UserId.New());
        }

        return (provider, handler, uow);
    }

    [Fact]
    public async Task TC_F02_03_CommandWithInvalidInput_ReturnsValidationError_HandlerNotCalled()
    {
        var (sp, handler, uow) = Build();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.Send(new CreateSample(""));

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error.Details.ShouldContainKey("Name");
        handler.Calls.ShouldBe(0);
        await uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TC_F02_04_CommandSentTwiceWithSameIdempotencyKey_ReturnsStoredResult_HandlerCalledOnce()
    {
        var (sp, handler, _) = Build();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var first = await dispatcher.Send(new CreateSample("acme", IdempotencyKey: "key-1"));
        var second = await dispatcher.Send(new CreateSample("acme", IdempotencyKey: "key-1"));

        first.IsSuccess.ShouldBeTrue();
        second.Value.ShouldBe(first.Value);
        handler.Calls.ShouldBe(1);

        var third = await dispatcher.Send(new CreateSample("acme", IdempotencyKey: "key-2"));
        third.Value.ShouldNotBe(first.Value);
        handler.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task IdempotencyKeys_AreScopedPerTenant()
    {
        var store = new Platform.Application.Idempotency.InMemoryIdempotencyStore();
        var acme = TenantId.New();
        var globex = TenantId.New();
        async Task<Result<Guid>> SendAs(TenantId tenant)
        {
            var services = new ServiceCollection().AddLogging();
            services.AddSingleton<Platform.Application.Idempotency.IIdempotencyStore>(store);
            services.AddModuleHandlers(typeof(PipelineTests).Assembly);
            await using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextSetter>().Set(tenant);
            return await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new CreateSample("x", "same-key"));
        }

        var a = await SendAs(acme);
        var b = await SendAs(globex);
        a.Value.ShouldNotBe(b.Value);
    }

    [Fact]
    public async Task TenantGuard_RejectsTenantScopedRequestWithoutTenant_AllowsTenantAgnostic()
    {
        var (sp, handler, _) = Build(withTenant: false);
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.Send(new CreateSample("acme"));
        result.Error.ShouldBe(Error.Common.TenantRequired);
        handler.Calls.ShouldBe(0);

        (await dispatcher.Send(new PlatformPing())).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task UnitOfWork_CommitsOnSuccess_RollsBackOnFailureAndException()
    {
        var (sp, handler, uow) = Build();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        (await dispatcher.Send(new CreateSample("ok"))).IsSuccess.ShouldBeTrue();
        await uow.Received(1).BeginAsync(Arg.Any<CancellationToken>());
        await uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        handler.Behavior = _ => Error.Conflict("sample.duplicate", "dup");
        (await dispatcher.Send(new CreateSample("dup"))).Error!.Code.ShouldBe("sample.duplicate");
        await uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());

        handler.Behavior = _ => throw new InvalidOperationException("boom");
        await Should.ThrowAsync<InvalidOperationException>(() => dispatcher.Send(new CreateSample("boom")));
        await uow.Received(2).RollbackAsync(Arg.Any<CancellationToken>());
        sp.GetRequiredService<OutboxBuffer>().Pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnitOfWork_ConcurrencyConflict_BecomesConflictError()
    {
        var (sp, _, uow) = Build();
        uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConcurrencyConflictException("x")));

        var result = await sp.GetRequiredService<IDispatcher>().Send(new CreateSample("ok"));

        result.Error.ShouldBe(Error.Common.ConcurrencyConflict);
        await uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Queries_DoNotRunInUnitOfWork()
    {
        var (sp, _, uow) = Build();
        var result = await sp.GetRequiredService<IDispatcher>().Send(new GetSample(Guid.NewGuid()));
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        await uow.DidNotReceive().BeginAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuditableCommand_QueuesAuditEventWithActorTargetAndSnapshots()
    {
        var tenant = TenantId.New();
        var (sp, handler, _) = Build(tenant);
        var scope = sp.GetRequiredService<IAuditScope>();
        handler.Behavior = _ =>
        {
            scope.Target = "sample:1";
            scope.Before = new { name = "old" };
            scope.After = new { name = "new" };
            return Result.Success(Guid.NewGuid());
        };

        (await sp.GetRequiredService<IDispatcher>().Send(new CreateSample("ok"))).IsSuccess.ShouldBeTrue();

        var audit = sp.GetRequiredService<OutboxBuffer>().Pending.OfType<AuditRecordedIntegrationEvent>().Single();
        audit.Action.ShouldBe("sample.create");
        audit.TenantId.ShouldBe(tenant.Value);
        audit.ActorId.ShouldNotBeNull();
        audit.Target.ShouldBe("sample:1");
        audit.BeforeJson.ShouldBe("{\"name\":\"old\"}");
        audit.AfterJson.ShouldBe("{\"name\":\"new\"}");
    }

    [Fact]
    public async Task PipelineOrder_IsFixed_AndModuleBehaviorsRunInnermost()
    {
        var services = new ServiceCollection().AddLogging();
        var trace = new List<string>();
        services.AddSingleton(trace);
        services.AddPipelineBehavior(typeof(TracingBehavior<,>));
        services.AddModuleHandlers(typeof(PipelineTests).Assembly);
        await using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<ITenantContextSetter>().Set(TenantId.New());

        var stages = sp.GetServices<IPipelineBehavior<CreateSample, Guid>>().OrderBy(b => b.Stage).Select(b => b.Stage).ToList();
        stages.ShouldBe([PipelineStage.Logging, PipelineStage.Validation, PipelineStage.TenantGuard, PipelineStage.Idempotency, PipelineStage.UnitOfWork, PipelineStage.Audit, PipelineStage.Module]);

        (await sp.GetRequiredService<IDispatcher>().Send(new CreateSample("ok"))).IsSuccess.ShouldBeTrue();
        trace.ShouldBe(["module"]);
    }

    [Fact]
    public async Task Dispatcher_WithoutHandler_Throws_AndDuplicateHandlersAreRejected()
    {
        var services = new ServiceCollection().AddLogging().AddPlatformApplication();
        await using var sp = services.BuildServiceProvider();
        sp.GetRequiredService<ITenantContextSetter>().Set(TenantId.New());
        await Should.ThrowAsync<InvalidOperationException>(() => sp.GetRequiredService<IDispatcher>().Send(new GetSample(Guid.NewGuid())));

        var twice = new ServiceCollection().AddModuleHandlers(typeof(PipelineTests).Assembly);
        Should.Throw<InvalidOperationException>(() => twice.AddModuleHandlers(typeof(PipelineTests).Assembly));
    }

    [Fact]
    public async Task DomainEventDispatcher_InvokesAllHandlersInOrder()
    {
        var services = new ServiceCollection().AddPlatformApplication();
        var seen = new List<string>();
        services.AddSingleton(seen);
        services.AddScoped<IDomainEventHandler<SampleCreated>, RecordingHandler>();
        await using var sp = services.BuildServiceProvider();
        var aggregate = new SampleAggregate(SampleId.New(), "x");

        await sp.GetRequiredService<IDomainEventDispatcher>().DispatchAsync(aggregate.DequeueEvents(), CancellationToken.None);

        seen.ShouldBe(["created"]);
    }

    [Fact]
    public void TC_F02_05_HandlerReturnsNotFound_ApiMapsTo404ProblemDetailsWithErrorCode()
    {
        Result<string> result = Error.NotFound("org.node.not_found", "Node not found.");

        var http = result.ToHttpResult();

        var problem = http.ShouldBeAssignableTo<IStatusCodeHttpResult>()!;
        problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        var details = ((IValueHttpResult)http).Value.ShouldBeAssignableTo<ProblemDetails>()!;
        details.Status.ShouldBe(404);
        details.Type.ShouldBe("urn:platform:error:org.node.not_found");
        details.Extensions["code"].ShouldBe("org.node.not_found");
        details.Detail.ShouldBe("Node not found.");
    }

    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Forbidden, 403)]
    public void ProblemDetails_StatusPerErrorType(ErrorType type, int status)
    {
        ProblemDetailsMapping.StatusCodeFor(type).ShouldBe(status);
        ProblemDetailsMapping.ToProblemDetails(new Error("x.y", "m", type)).Status.ShouldBe(status);
    }

    [Fact]
    public void ProblemDetails_ValidationErrors_IncludeFieldDetails_AndSuccessMaps()
    {
        var error = Error.Validation("platform.validation_failed", "bad", new Dictionary<string, string[]> { ["name"] = ["required"] });
        var details = ProblemDetailsMapping.ToProblemDetails(error).ShouldBeOfType<ValidationProblemDetails>();
        details.Errors["name"].ShouldBe(["required"]);

        Result.Success("v").ToHttpResult().ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(200);
        Result.Success().ToHttpResult().ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(204);
        ((Result)Error.Conflict("c", "m")).ToHttpResult().ShouldBeAssignableTo<IStatusCodeHttpResult>()!.StatusCode.ShouldBe(409);
    }

    private sealed class TracingBehavior<TRequest, TResponse>(List<string> trace) : IPipelineBehavior<TRequest, TResponse>
    {
        public int Stage => PipelineStage.Module;

        public Task<Result<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            trace.Add("module");
            return next();
        }
    }

    private sealed class RecordingHandler(List<string> seen) : IDomainEventHandler<SampleCreated>
    {
        public Task Handle(SampleCreated domainEvent, CancellationToken cancellationToken)
        {
            seen.Add("created");
            return Task.CompletedTask;
        }
    }
}
