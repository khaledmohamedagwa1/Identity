using System.Collections.Concurrent;
using System.Reflection;

namespace Platform.Contracts;

/// <summary>Reads <see cref="IntegrationEventAttribute"/> metadata from event types.</summary>
public static class IntegrationEventMetadata
{
    private static readonly ConcurrentDictionary<Type, IntegrationEventAttribute> Cache = new();

    /// <summary>Returns the metadata for <paramref name="eventType"/>.</summary>
    /// <exception cref="InvalidOperationException">The type has no <see cref="IntegrationEventAttribute"/>.</exception>
    public static IntegrationEventAttribute For(Type eventType) =>
        Cache.GetOrAdd(eventType, static t =>
            t.GetCustomAttribute<IntegrationEventAttribute>()
            ?? throw new InvalidOperationException(
                $"Integration event '{t.FullName}' must declare [IntegrationEvent(name, version)]."));

    /// <summary>Returns the outbox type key for <typeparamref name="TEvent"/>.</summary>
    public static string TypeKeyOf<TEvent>() where TEvent : IIntegrationEvent => For(typeof(TEvent)).TypeKey;
}
