using Microsoft.EntityFrameworkCore;

namespace Platform.Infrastructure.Persistence;

/// <summary>Owns the shared <c>platform</c> schema: outbox, inbox and idempotency keys.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options, ModuleDbContextServices services)
    : ModuleDbContext(options, services)
{
    /// <summary>Schema name.</summary>
    public const string SchemaName = PlatformTables.Schema;

    /// <inheritdoc />
    public override string Schema => SchemaName;

    /// <inheritdoc />
    protected override bool OwnsPlatformSchema => true;

    /// <inheritdoc />
    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
    }
}
