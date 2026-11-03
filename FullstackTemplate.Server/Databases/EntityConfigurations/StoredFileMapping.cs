namespace FullstackTemplate.Server.Databases.EntityConfigurations;

using Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class StoredFileMapping
{
    /// <summary>
    /// Maps a <see cref="StoredFile"/> to the columns <c>file_name</c>, <c>media_type</c>, <c>size_bytes</c>, and
    /// <c>storage_key</c>: <c>builder.ComplexProperty(e => e.File, file => file.MapStoredFile());</c>
    /// </summary>
    public static void MapStoredFile(this ComplexPropertyBuilder<StoredFile> file)
    {
        file.Property(f => f.Name)
            .HasColumnName("file_name")
            .HasMaxLength(StoredFile.NameMaxLength)
            .IsRequired();
        file.Property(f => f.MediaType)
            .HasColumnName("media_type")
            .HasMaxLength(StoredFile.MediaTypeMaxLength)
            .IsRequired();
        file.Property(f => f.SizeBytes)
            .HasColumnName("size_bytes");
        file.Property(f => f.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(StoredFile.StorageKeyMaxLength)
            .IsRequired();
    }
}
