namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FirstName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.LastName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Identifier)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.Username)
            .IsRequired()
            .HasMaxLength(100);

        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(320);
        });

        // Indexes
        builder.HasIndex(e => e.Identifier)
            .IsUnique();

        builder.HasIndex(e => e.Username);
    }
}
