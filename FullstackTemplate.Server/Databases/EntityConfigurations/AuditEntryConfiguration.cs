namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    /// <summary>The <c>xmin</c> of an entry. The triggers refuse a change of an entry, so it is the id of the transaction of its save.</summary>
    internal const string TransactionId = nameof(TransactionId);

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_audit_entries_action_names_an_action", "(kind = 'Action') = (action IS NOT NULL)"));

        // occurred_at is in the key, so the table can become partitioned by time with no change of key. A backward scan
        // of the key gives the list of all tenants, newest first.
        builder.HasKey(e => new { e.OccurredAt, e.RecordId, e.Ordinal });
        builder.Property<uint>(TransactionId).HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate();

        builder.Property(e => e.ActorUserId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
        builder.Property(e => e.ActorAgentId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
        builder.Property(e => e.RequestName).HasMaxLength(200);
        builder.Property(e => e.TraceId).HasMaxLength(32).IsFixedLength();
        builder.Property(e => e.TenantId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
        builder.Property(e => e.RootType).HasMaxLength(100);
        builder.Property(e => e.RootId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
        builder.Property(e => e.EntityType).HasMaxLength(100);
        builder.Property(e => e.EntityId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Action).HasMaxLength(100);
        builder.Property(e => e.Data).HasColumnType("jsonb");

        // The history of one aggregate, newest first. A root id is unique across types, because each id has its prefix.
        builder.HasIndex(e => new { e.RootId, e.OccurredAt, e.RecordId, e.Ordinal })
            .IsDescending(false, true, true, true);
        // The list of one tenant, newest first.
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt, e.RecordId, e.Ordinal })
            .IsDescending(false, true, true, true);

        // No foreign keys: an audit entry outlives its entity and its entity type.
    }
}
