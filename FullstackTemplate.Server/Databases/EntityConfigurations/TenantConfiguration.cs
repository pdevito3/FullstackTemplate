namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(Tenant.NameMaxLength);

        // Indexes
        builder.HasIndex(e => e.Name)
            .IsUnique()
            .ForRequestField("name", Tenant.NameInUseMessage);
    }
}
