using System.Text.Json;
using Platform.Domain.Ids;
using Platform.Domain.Results;

namespace Platform.BuildingBlocks.Tests;

public sealed class DomainTests
{
    [Fact]
    public void TC_F02_01_ValueObjectsWithSameValues_AreEqualWithSameHashCode()
    {
        var a = new Money(10m, "egp");
        var b = new Money(10m, "EGP");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        (a != b).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.Equals((object)new Money(11m, "EGP")).ShouldBeFalse();
        a.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void TC_F02_02_AggregateRaisesTwoEvents_ThenClearEvents_ReturnsInOrderThenEmpty()
    {
        var aggregate = new SampleAggregate(SampleId.New(), "first");
        aggregate.Rename("second");

        aggregate.DomainEvents.Count.ShouldBe(2);
        aggregate.DomainEvents[0].ShouldBeOfType<SampleCreated>();
        aggregate.DomainEvents[1].ShouldBeOfType<SampleRenamed>().Name.ShouldBe("second");

        aggregate.ClearEvents();
        aggregate.DomainEvents.ShouldBeEmpty();

        aggregate.Rename("third");
        var dequeued = aggregate.DequeueEvents();
        dequeued.Single().ShouldBeOfType<SampleRenamed>();
        aggregate.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void TC_F02_06_GenerateTenThousandIds_AllUniqueAndTimeOrdered()
    {
        var ids = Enumerable.Range(0, 10_000).Select(_ => Uuid7.NewGuid()).ToList();

        ids.Distinct().Count().ShouldBe(10_000);
        var asStrings = ids.Select(g => g.ToString()).ToList();
        asStrings.ShouldBe(asStrings.Order(StringComparer.Ordinal).ToList(), "UUIDv7 values must be strictly time-ordered");
        ids.ShouldAllBe(g => g.Version == 7);
        ids.ShouldAllBe(g => (g.Variant & 0b1100) == 0b1000);
    }

    [Fact]
    public void Uuid7_EmbedsTimestamp_AndStaysOrderedWhenClockStandsStill()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var ids = Enumerable.Range(0, 5000).Select(_ => Uuid7.NewGuid(clock)).Select(g => g.ToString()).ToList();
        ids.ShouldBe(ids.Order(StringComparer.Ordinal).ToList());
        Uuid7.GetTimestamp(Guid.Parse(ids[0])).ShouldBeGreaterThanOrEqualTo(clock.GetUtcNow());
    }

    [Fact]
    public void Entities_AreEqualByTypeAndId()
    {
        var id = SampleId.New();
        var a = new SampleAggregate(id, "a");
        var b = new SampleAggregate(id, "b");
        var c = new SampleAggregate(SampleId.New(), "a");

        (a == b).ShouldBeTrue();
        (a != c).ShouldBeTrue();
        a.Equals((object)b).ShouldBeTrue();
        a.Equals(null).ShouldBeFalse();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Result_SuccessAndFailure_BehaveAsExpected()
    {
        Result<int> ok = 5;
        ok.IsSuccess.ShouldBeTrue();
        ok.Map(v => v * 2).Value.ShouldBe(10);
        ok.Bind(v => Result.Success(v.ToString())).Value.ShouldBe("5");

        Result<int> failed = Error.Conflict("x.conflict", "conflict");
        failed.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => _ = failed.Value);
        failed.Map(v => v * 2).Error!.Code.ShouldBe("x.conflict");
        failed.Bind(v => Result.Success(v)).IsFailure.ShouldBeTrue();

        Result plain = Error.Forbidden("x.forbidden", "no");
        plain.IsFailure.ShouldBeTrue();
        Result.Success().IsSuccess.ShouldBeTrue();
        Result.Failure<string>(Error.NotFound("x", "y")).Error!.Type.ShouldBe(ErrorType.NotFound);
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Fact]
    public void Error_FactoriesSetTypeAndDetails()
    {
        Error.Validation("v", "m", new Dictionary<string, string[]> { ["name"] = ["required"] }).Details["name"].ShouldBe(["required"]);
        Error.Validation("v", "m").Details.ShouldBeEmpty();
        Error.NotFound("n", "m").Type.ShouldBe(ErrorType.NotFound);
        Error.Conflict("c", "m").Type.ShouldBe(ErrorType.Conflict);
        Error.Forbidden("f", "m").Type.ShouldBe(ErrorType.Forbidden);
        Error.Common.ConcurrencyConflict.Type.ShouldBe(ErrorType.Conflict);
        Error.Common.TenantRequired.Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void StronglyTypedIds_RoundTripThroughJson_AsPlainUuid()
    {
        var options = new JsonSerializerOptions { Converters = { new StronglyTypedIdJsonConverterFactory() } };
        var tenant = TenantId.New();
        var json = JsonSerializer.Serialize(new { Tenant = tenant, Map = new Dictionary<TenantId, int> { [tenant] = 1 } }, options);
        json.ShouldContain($"\"{tenant.Value}\"");

        JsonSerializer.Deserialize<TenantId>($"\"{tenant.Value}\"", options).ShouldBe(tenant);
        JsonSerializer.Deserialize<Dictionary<TenantId, int>>($"{{\"{tenant.Value}\":3}}", options)![tenant].ShouldBe(3);
        new StronglyTypedIdJsonConverterFactory().CanConvert(typeof(Guid)).ShouldBeFalse();
        UserId.From(tenant.Value).ToString().ShouldBe(tenant.Value.ToString());
        tenant.ToString().ShouldBe(tenant.Value.ToString());
        UserId.New().Value.Version.ShouldBe(7);
    }
}
