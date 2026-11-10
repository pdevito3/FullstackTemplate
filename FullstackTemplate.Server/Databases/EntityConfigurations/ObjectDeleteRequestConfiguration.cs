namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Attachments;
using Domain.ObjectDeleteRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ObjectDeleteRequestConfiguration : IEntityTypeConfiguration<ObjectDeleteRequest>
{
    public void Configure(EntityTypeBuilder<ObjectDeleteRequest> builder)
    {
        builder.ToTable("object_delete_requests");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        // No foreign key: the request outlives the tenant and the row that named the object
        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.Key)
            .IsRequired()
            .HasMaxLength(StoredFile.StorageKeyMaxLength);

        builder.Property(e => e.LastError)
            .HasMaxLength(ObjectDeleteRequest.LastErrorMaxLength);

        // The claim reads the due requests that are not dead
        builder.HasIndex(e => new { e.DueAt, e.Id })
            .HasFilter("dead_at IS NULL");
    }
}
