using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FullstackTemplate.Bff.BackChannel;

public sealed class UsedLogoutTokenConfiguration : IEntityTypeConfiguration<UsedLogoutToken>
{
    /// <summary>
    /// The longest <c>jti</c> that the replay cache keeps. A logout token with a longer <c>jti</c> is rejected.
    /// </summary>
    public const int JtiMaxLength = 255;

    public void Configure(EntityTypeBuilder<UsedLogoutToken> builder)
    {
        builder.HasKey(e => e.Jti);
        builder.Property(e => e.Jti)
            .HasMaxLength(JtiMaxLength)
            .UseCollation("C");

        // The cleanup deletes on this column
        builder.HasIndex(e => e.Expires);
    }
}
