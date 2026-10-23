namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Agents;
using Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
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
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        // A client ID is case-sensitive, so it compares bytes like an ID.
        builder.Property(e => e.ClientId)
            .IsRequired()
            .HasMaxLength(255)
            .UseCollation(EntityId.Collation);

        builder.ComplexProperty(e => e.Mode, mode =>
        {
            mode.Property(m => m.Value)
                .HasColumnName("mode")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.HasMany(e => e.Permissions)
            .WithOne()
            .HasForeignKey(ap => ap.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Permissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        // A deleted agent frees its client ID, so the unique indexes cover only live rows.
        builder.HasIndex(e => new { e.TenantId, e.ClientId })
            .IsUnique()
            .HasFilter("is_deleted = false");

        // The token of a machine client names no tenant, so its client ID has one live machine agent in all tenants.
        builder.HasIndex(e => e.ClientId)
            .IsUnique()
            .HasFilter($"mode = '{AgentMode.MachineValue}' AND is_deleted = false");

        // The default sort of the agent list.
        builder.HasIndex(e => new { e.TenantId, e.Name, e.Id });
    }
}
