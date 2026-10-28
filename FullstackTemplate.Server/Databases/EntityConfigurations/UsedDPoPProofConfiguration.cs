namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Domain.DPoPProofs;

public sealed class UsedDPoPProofConfiguration : IEntityTypeConfiguration<UsedDPoPProof>
{
    public void Configure(EntityTypeBuilder<UsedDPoPProof> builder)
    {
        builder.ToTable("used_dpop_proofs");
        builder.HasKey(e => e.Id);
        // A base64url SHA-256 has 43 characters
        builder.Property(e => e.Id)
            .HasMaxLength(64)
            .UseCollation("C");

        // The cleanup deletes on this column
        builder.HasIndex(e => e.Expires);
    }
}
