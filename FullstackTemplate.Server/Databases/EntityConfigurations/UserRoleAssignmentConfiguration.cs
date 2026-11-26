namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserRoleAssignmentConfiguration : IEntityTypeConfiguration<UserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<UserRoleAssignment> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.UserId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.ComplexProperty(e => e.Role, role =>
        {
            role.Property(r => r.Value)
                .HasColumnName("role")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.HasIndex(e => e.UserId);

        builder.ApplyTenanting(e => e.User.TenantId);
    }
}
