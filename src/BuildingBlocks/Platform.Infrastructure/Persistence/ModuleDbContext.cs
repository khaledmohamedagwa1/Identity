using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Events;
using Platform.Application.Persistence;
using Platform.Application.Tenancy;
using Platform.Domain.Ids;
using Platform.Domain.Primitives;
using Platform.Infrastructure.Outbox;

namespace Platform.Infrastructure.Persistence;

/// <summary>Services every module context needs; registered scoped by <c>AddPlatformInfrastructure</c>.</summary>
public sealed class ModuleDbContextServices(
    ITenantContext tenant,
    IDomainEventDispatcher domainEvents,
    OutboxBuffer outbox,
    EfUnitOfWork unitOfWork)
{
    /// <summary>The current tenant.</summary>
    public ITenantContext Tenant { get; } = tenant;

    /// <summary>In-process domain event dispatcher.</summary>
    public IDomainEventDispatcher DomainEvents { get; } = domainEvents;

    /// <summary>Integration events queued by handlers.</summary>
    public OutboxBuffer Outbox { get; } = outbox;

    /// <summary>The unit of work of the current scope.</summary>
    public EfUnitOfWork UnitOfWork { get; } = unitOfWork;
}

/// <summary>
/// Base class for each module's DbContext (F-03):
/// <list type="bullet">
/// <item>its own PostgreSQL schema (section 6.5);</item>
/// <item>converters for strongly typed ids;</item>
/// <item>a global query filter on <c>TenantId</c> for every <see cref="ITenantOwned"/> entity;</item>
/// <item>optimistic concurrency on <see cref="IAggregateRoot.Version"/>;</item>
/// <item>domain events dispatched before commit; integration events written to <c>platform.outbox</c> in the same transaction;</item>
/// <item>snake_case naming (section 7.4).</item>
/// </list>
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    private const int MaxDomainEventRounds = 10;
    private readonly ModuleDbContextServices _services;

    /// <summary>Creates the context and enlists it in the scope's unit of work.</summary>
    protected ModuleDbContext(DbContextOptions options, ModuleDbContextServices services) : base(options)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        services.UnitOfWork.Enlist(this);
    }

    /// <summary>The module's schema, for example <c>org</c>.</summary>
    public abstract string Schema { get; }

    /// <summary>Assemblies containing the module's strongly typed ids (usually its Domain project).</summary>
    protected virtual IEnumerable<Assembly> IdAssemblies => [GetType().Assembly];

    /// <summary>True only for the context that owns migrations of the shared <c>platform</c> schema.</summary>
    protected virtual bool OwnsPlatformSchema => false;

    /// <summary>Pending and processed outbox rows.</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Tenant used by the global query filter. Without a tenant context it is the empty UUID, so tenant
    /// tables return no rows (fail closed).
    /// </summary>
    public TenantId CurrentTenantId => _services.Tenant.TenantId ?? TenantId.From(Guid.Empty);

    /// <summary>Configures the module's entities.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    /// <inheritdoc />
    protected sealed override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.AddStronglyTypedIds([typeof(TenantId).Assembly, .. IdAssemblies]);
        configurationBuilder.Properties<DateTimeOffset>().HaveColumnType("timestamp with time zone");
    }

    /// <inheritdoc />
    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        ConfigureModel(modelBuilder);
        PlatformTables.MapOutbox(modelBuilder, excludeFromMigrations: !OwnsPlatformSchema);
        if (OwnsPlatformSchema)
        {
            PlatformTables.MapInboxAndIdempotency(modelBuilder);
        }

        ApplyTenantFilters(modelBuilder);
        ApplyConcurrencyTokens(modelBuilder);
        SnakeCase.Apply(modelBuilder);
    }

    /// <summary>Synchronous saves are not supported: they would bypass async interceptors and domain events.</summary>
    public sealed override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync.");

    /// <inheritdoc />
    public sealed override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken).ConfigureAwait(false);

        foreach (var integrationEvent in _services.Outbox.Drain())
        {
            OutboxMessages.Add(OutboxMessage.From(integrationEvent));
        }

        foreach (var entry in ChangeTracker.Entries<IAggregateRoot>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.IncrementVersion();
            }
        }

        await _services.UnitOfWork.EnsureTransactionAsync(this, cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("The aggregate was modified by another transaction.", ex);
        }
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxDomainEventRounds; round++)
        {
            var events = ChangeTracker.Entries<IAggregateRoot>()
                .SelectMany(e => e.Entity.DequeueEvents())
                .ToList();
            if (events.Count == 0)
            {
                return;
            }

            await _services.DomainEvents.DispatchAsync(events, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Domain events kept raising new events after {MaxDomainEventRounds} rounds.");
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var context = Expression.Constant(this);
        foreach (var entity in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType) && t.BaseType is null && !t.IsOwned()))
        {
            var parameter = Expression.Parameter(entity.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(parameter, nameof(ITenantOwned.TenantId)),
                Expression.Property(context, nameof(CurrentTenantId)));
            modelBuilder.Entity(entity.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    private static void ApplyConcurrencyTokens(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes()
                     .Where(t => typeof(IAggregateRoot).IsAssignableFrom(t.ClrType) && t.BaseType is null))
        {
            modelBuilder.Entity(entity.ClrType).Property(nameof(IAggregateRoot.Version)).IsConcurrencyToken();
        }
    }
}
