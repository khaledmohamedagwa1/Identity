using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Application.Auditing;
using Platform.Application.Behaviors;
using Platform.Application.Events;
using Platform.Application.Idempotency;
using Platform.Application.Messaging;
using Platform.Application.Persistence;
using Platform.Application.Pipeline;
using Platform.Application.Tenancy;

namespace Platform.Application;

/// <summary>Registers the dispatcher, the fixed pipeline and module handlers.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the dispatcher and the built-in pipeline behaviours in their fixed order
    /// (<see cref="PipelineStage"/>). Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(d => d.ServiceType == typeof(IDispatcher)))
        {
            return services;
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<OutboxBuffer>();
        services.AddScoped<IOutbox>(sp => sp.GetRequiredService<OutboxBuffer>());
        services.AddScoped<IAuditScope, AuditScope>();
        services.TryAddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();

        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TenantGuardBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(IdempotencyBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        return services;
    }

    /// <summary>
    /// Registers every request handler, domain event handler and FluentValidation validator found in
    /// <paramref name="assembly"/>. A request type with more than one handler is a startup error.
    /// </summary>
    public static IServiceCollection AddModuleHandlers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);
        services.AddPlatformApplication();

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                var definition = contract.GetGenericTypeDefinition();
                if (definition == typeof(IRequestHandler<,>))
                {
                    if (services.Any(d => d.ServiceType == contract))
                    {
                        throw new InvalidOperationException(
                            $"More than one handler registered for '{contract.GetGenericArguments()[0].FullName}'.");
                    }

                    services.AddScoped(contract, type);
                }
                else if (definition == typeof(IDomainEventHandler<>) || definition == typeof(IValidator<>))
                {
                    services.AddScoped(contract, type);
                }
            }
        }

        return services;
    }

    /// <summary>Adds a module-specific pipeline behaviour; it runs at <see cref="PipelineStage.Module"/> unless it says otherwise.</summary>
    public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type openGenericBehavior)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped(typeof(IPipelineBehavior<,>), openGenericBehavior);
        return services;
    }
}
