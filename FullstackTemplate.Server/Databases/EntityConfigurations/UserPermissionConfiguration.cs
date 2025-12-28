namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.UserId)
            .IsRequired();

        builder.OwnsOne(e => e.Permission, permission =>
        {
            permission.Property(p => p.Value)
                .HasColumnName("permission")
                .HasMaxLength(100)
                .IsRequired();

            permission.HasIndex(p => p.Value);
        });

        builder.HasIndex(e => e.UserId);
    }
}
