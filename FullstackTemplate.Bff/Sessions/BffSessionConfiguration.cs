using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FullstackTemplate.Bff.Sessions;

public sealed class BffSessionConfiguration : IEntityTypeConfiguration<BffSession>
{
    /// <summary>
    /// The length of a key: 32 random bytes in base64url.
    /// </summary>
    public const int KeyLength = 43;

    public void Configure(EntityTypeBuilder<BffSession> builder)
    {
        builder.HasKey(e => e.Key);
        builder.Property(e => e.Key)
            .HasMaxLength(KeyLength)
            .UseCollation("C");

        builder.Property(e => e.SubjectId)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(e => e.SessionId)
            .HasMaxLength(255);

        builder.Property(e => e.Ticket)
            .IsRequired();

        // The Postgres system column. A write with an old value changes no row.
        builder.Property(e => e.Version)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.Ignore(e => e.EndsAt);

        builder.HasIndex(e => e.SubjectId);
        builder.HasIndex(e => e.SessionId);
        // The cleanup deletes on these two columns
        builder.HasIndex(e => e.Expires);
        builder.HasIndex(e => e.AbsoluteExpires);
    }
}
