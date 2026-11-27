using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Application;
using Platform.Application.Idempotency;
using Platform.Application.Persistence;
using Platform.Infrastructure.Idempotency;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Tenancy;

namespace Platform.Infrastructure;

/// <summary>Registers infrastructure building blocks.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds the EF unit of work, RLS interceptor, platform schema context and PostgreSQL idempotency store.
    /// <paramref name="connectionString"/> must use the application role (no BYPASSRLS, not the table owner).
    /// </summary>
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddPlatformApplication();
        services.TryAddScoped<EfUnitOfWork>();
        services.Replace(ServiceDescriptor.Scoped<IUnitOfWork>(sp => sp.GetRequiredService<EfUnitOfWork>()));
        services.TryAddScoped<ModuleDbContextServices>();
        services.TryAddScoped<TenantRlsInterceptor>();
        services.AddModuleDbContext<PlatformDbContext>(connectionString, PlatformDbContext.SchemaName);
        services.Replace(ServiceDescriptor.Scoped<IIdempotencyStore, PostgresIdempotencyStore>());
        return services;
    }

    /// <summary>Registers a module DbContext on PostgreSQL with the RLS interceptor and a per-schema migrations history table.</summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string connectionString, string schema)
        where TContext : ModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddDbContext<TContext>((sp, options) => options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .AddInterceptors(sp.GetRequiredService<TenantRlsInterceptor>()));
        return services;
    }
}
