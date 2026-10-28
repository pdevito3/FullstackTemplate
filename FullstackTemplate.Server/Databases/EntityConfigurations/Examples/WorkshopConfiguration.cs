namespace FullstackTemplate.Server.Databases.EntityConfigurations.Examples;

using Domain;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Server.Examples.Venues;
using Server.Examples.Workshops;

public sealed class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
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

        builder.Property(e => e.VenueId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        // The venue is its own aggregate, so a save of a workshop never writes it
        builder.HasOne<Venue>()
            .WithMany()
            .HasForeignKey(e => e.VenueId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(Workshop.TitleMaxLength);

        builder.ComplexProperty(e => e.Status, status =>
        {
            status.Property(s => s.Value)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();
        });

        // The default sort of the workshop list. TenantId comes first, so this index also serves each query on the tenant alone.
        builder.HasIndex(e => new { e.TenantId, e.StartsAt, e.Id });
    }
}
