namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.UserId)
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

        builder.HasIndex(e => e.UserId);

        builder.ApplyTenanting(e => e.User.TenantId);
    }
}
