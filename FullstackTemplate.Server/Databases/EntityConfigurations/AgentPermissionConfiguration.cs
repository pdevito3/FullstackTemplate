namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class AgentPermissionConfiguration : IEntityTypeConfiguration<AgentPermission>
{
    public void Configure(EntityTypeBuilder<AgentPermission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.AgentId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.ComplexProperty(e => e.Permission, permission =>
        {
            permission.Property(p => p.Value)
                .HasColumnName("permission")
                .HasMaxLength(100)
                .IsRequired();
        });

        builder.HasIndex(e => e.AgentId);

        builder.ApplyTenanting(e => e.Agent.TenantId);
    }
}
