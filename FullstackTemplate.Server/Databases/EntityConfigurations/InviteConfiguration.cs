namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain;
using Domain.EmailAddresses;
using Domain.Invites;
using Domain.Tenants;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
    // The hex of a SHA-256 hash
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<Invite> builder)
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

        builder.ComplexProperty(e => e.Role, role =>
        {
            role.Property(r => r.Value)
                .HasColumnName("role")
                .HasMaxLength(50)
                .IsRequired();
        });

        // An invite for anyone has no email
        builder.ComplexProperty(e => e.Email, email =>
        {
            email.Property(e => e.Value)
                .HasColumnName("email")
                .HasMaxLength(EmailAddress.MaxLength);
        });

        builder.Property(e => e.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength);

        builder.ComplexProperty(e => e.Status, status =>
        {
            status.Property(s => s.Value)
                .HasColumnName("status")
                .HasMaxLength(50)
                .IsRequired();
        });

        builder.Property(e => e.ClaimedByUserId)
            .HasMaxLength(EntityId.MaxLength)
            .UseCollation(EntityId.Collation);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.ClaimedByUserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // A preview and a claim find the invite by the hash of the token. A collision of two hashes is not a real
        // case, so the index is not unique, and a unique index would need a message for a field that no caller sends.
        builder.HasIndex(e => e.TokenHash);

        // The default sort of the invite list. TenantId comes first, so this index also serves each query on the tenant alone.
        builder.HasIndex(e => new { e.TenantId, e.ExpiresAt, e.Id });
    }
}
