using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Platform.Domain.Ids;

namespace Platform.Infrastructure.Persistence;

/// <summary>Stores a strongly typed id as a PostgreSQL <c>uuid</c>.</summary>
public sealed class StronglyTypedIdConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, value => Create(value))
    where TId : struct, IStronglyTypedId<TId>
{
    // Expression trees cannot call static abstract members directly, so route through a helper.
    private static TId Create(Guid value) => TId.From(value);
}

/// <summary>Registers converters for every strongly typed id in the given assemblies.</summary>
public static class StronglyTypedIdConventions
{
    /// <summary>Applies <see cref="StronglyTypedIdConverter{TId}"/> to all ids declared in <paramref name="assemblies"/>.</summary>
    public static ModelConfigurationBuilder AddStronglyTypedIds(this ModelConfigurationBuilder builder, params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (var type in assemblies.Distinct().SelectMany(a => a.GetTypes()).Where(IsStronglyTypedId))
        {
            builder.Properties(type).HaveConversion(typeof(StronglyTypedIdConverter<>).MakeGenericType(type));
        }

        return builder;
    }

    private static bool IsStronglyTypedId(Type type) =>
        type is { IsValueType: true, IsGenericTypeDefinition: false } && type.GetInterfaces().Any(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStronglyTypedId<>) && i.GetGenericArguments()[0] == type);
}
