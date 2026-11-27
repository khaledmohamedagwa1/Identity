using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Idempotency;
using Platform.Infrastructure.Inbox;
using Platform.Infrastructure.Outbox;

namespace Platform.Infrastructure.Persistence;

/// <summary>Mapping of the shared <c>platform</c> schema tables.</summary>
internal static class PlatformTables
{
    public const string Schema = "platform";

    public static void MapOutbox(ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox", Schema, t => t.ExcludeFromMigrations(excludeFromMigrations));
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityByDefaultColumn();
            b.Property(x => x.Type).HasMaxLength(200).IsRequired();
            b.Property(x => x.Payload).HasColumnType("jsonb").IsRequired();
            b.HasIndex(x => x.EventId).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Id }).HasFilter("processed_at IS NULL").HasDatabaseName("ix_outbox_pending_by_tenant");
        });
    }

    public static void MapInboxAndIdempotency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox", Schema);
            b.HasKey(x => new { x.EventId, x.Consumer });
            b.Property(x => x.Consumer).HasMaxLength(200);
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_keys", Schema);
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(512);
            b.Property(x => x.ResponseType).HasMaxLength(512);
            b.Property(x => x.ValueJson).HasColumnType("jsonb");
        });
    }
}
