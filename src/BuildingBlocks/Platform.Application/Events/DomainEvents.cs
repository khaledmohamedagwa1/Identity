using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Platform.Domain.Primitives;

namespace Platform.Application.Events;

/// <summary>Handles a domain event in-process, before commit, in the same transaction.</summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    /// <summary>Handles the event.</summary>
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>Dispatches domain events to every registered <see cref="IDomainEventHandler{TEvent}"/>.</summary>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches the events in order.</summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken);
}

internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IDomainEvent, CancellationToken, Task>> Invokers = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);
        foreach (var domainEvent in domainEvents)
        {
            var invoker = Invokers.GetOrAdd(domainEvent.GetType(), static t =>
                typeof(DomainEventDispatcher)
                    .GetMethod(nameof(InvokeAsync), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(t)
                    .CreateDelegate<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>());
            await invoker(services, domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task InvokeAsync<TEvent>(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.Handle((TEvent)domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
