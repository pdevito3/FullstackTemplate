namespace FullstackTemplate.Server.Databases.EntityConfigurations.Examples;

using Domain;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Server.Examples.Workshops;

public sealed class WorkshopAttachmentConfiguration : IEntityTypeConfiguration<WorkshopAttachment>
{
    public void Configure(EntityTypeBuilder<WorkshopAttachment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.Property(e => e.WorkshopId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        // An attachment is its own root, so an upload does not change the version of the workshop
        builder.HasOne<Workshop>()
            .WithMany()
            .HasForeignKey(e => e.WorkshopId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.Property(e => e.FileName)
            .IsRequired()
            .HasMaxLength(WorkshopAttachment.FileNameMaxLength);

        builder.Property(e => e.ContentType)
            .IsRequired()
            .HasMaxLength(WorkshopAttachment.ContentTypeMaxLength);

        builder.Ignore(e => e.StorageKey);
        builder.Ignore(e => e.HasPreview);

        // The list of a workshop, oldest first
        builder.HasIndex(e => new { e.WorkshopId, e.UploadedAt, e.Id });
    }
}
