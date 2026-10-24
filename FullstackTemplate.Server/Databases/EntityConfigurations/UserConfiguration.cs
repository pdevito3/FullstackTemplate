namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.EmailAddresses;
using Domain.Tenants;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(User.FirstNameMaxLength);

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(User.LastNameMaxLength);

        builder.Property(e => e.Identifier)
            .IsRequired()
            .HasMaxLength(User.IdentifierMaxLength);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(User.UsernameMaxLength);

        builder.Property(e => e.TenantId)
            .IsRequired()
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(EmailAddress.MaxLength)
                .IsRequired();
        });

        builder.ComplexProperty(e => e.Role, role =>
        {
            role.Property(r => r.Value)
                .HasColumnName("role")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.HasMany(e => e.UserPermissions)
            .WithOne(up => up.User)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.UserPermissions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(e => e.Identifier)
            .IsUnique();

        builder.HasIndex(e => e.Username);

        // The default sort of the user list. TenantId comes first, so this index also serves each query on the tenant alone.
        builder.HasIndex(e => new { e.TenantId, e.LastName, e.FirstName, e.Id });
    }
}
