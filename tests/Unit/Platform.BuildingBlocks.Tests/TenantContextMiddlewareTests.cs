using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Platform.Api;
using Platform.Application.Tenancy;

namespace Platform.BuildingBlocks.Tests;

public sealed class TenantContextMiddlewareTests
{
    [Fact]
    public async Task ReadsTenantUserAndActorFromTokenClaims_NeverFromHeaders()
    {
        var tid = Guid.NewGuid();
        var sub = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tid", tid.ToString()), new Claim("sub", sub.ToString()), new Claim("act", $"{{\"sub\":\"{actor}\"}}")],
                authenticationType: "test")),
        };
        http.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString();
        var context = new TenantContext();

        await new TenantContextMiddleware(_ => Task.CompletedTask).InvokeAsync(http, context);

        context.TenantId!.Value.Value.ShouldBe(tid);
        context.UserId!.Value.Value.ShouldBe(sub);
        context.ImpersonatorId!.Value.Value.ShouldBe(actor);
        ((ITenantContext)context).HasTenant.ShouldBeTrue();
    }

    [Fact]
    public async Task AnonymousOrMalformedClaims_LeaveContextEmpty()
    {
        var context = new TenantContext();
        await new TenantContextMiddleware(_ => Task.CompletedTask).InvokeAsync(new DefaultHttpContext(), context);
        context.TenantId.ShouldBeNull();

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tid", "not-a-guid"), new Claim("act", "{bad json")], "test")),
        };
        await new TenantContextMiddleware(_ => Task.CompletedTask).InvokeAsync(http, context);
        context.TenantId.ShouldBeNull();
        context.ImpersonatorId.ShouldBeNull();
    }

    [Fact]
    public void IdempotencyKeyHeader_IsReadAndLengthLimited()
    {
        var http = new DefaultHttpContext();
        http.GetIdempotencyKey().ShouldBeNull();
        http.Request.Headers[HttpContextExtensions.IdempotencyKeyHeader] = "abc";
        http.GetIdempotencyKey().ShouldBe("abc");
        http.Request.Headers[HttpContextExtensions.IdempotencyKeyHeader] = new string('x', 201);
        http.GetIdempotencyKey().ShouldBeNull();
    }
}
