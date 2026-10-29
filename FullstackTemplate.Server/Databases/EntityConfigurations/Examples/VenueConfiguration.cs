namespace FullstackTemplate.Server.Databases.EntityConfigurations.Examples;

using Domain;
using Domain.Tenants;
using Domain.TimeZones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Server.Examples.Venues;

public sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
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

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(Venue.NameMaxLength);

        builder.ComplexProperty(e => e.TimeZone, timeZone =>
        {
            timeZone.Property(t => t.Value)
                .HasColumnName("time_zone")
                .HasMaxLength(TimeZoneId.MaxLength)
                .IsRequired();
        });

        builder.HasIndex(e => new { e.TenantId, e.Name, e.Id });
    }
}
