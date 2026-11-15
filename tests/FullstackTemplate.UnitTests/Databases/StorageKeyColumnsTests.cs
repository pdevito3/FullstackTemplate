namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases.EntityConfigurations;
using FullstackTemplate.Server.Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class StorageKeyColumnsTests
{
    [Test]
    public void each_stored_file_of_the_model_gives_a_column_also_off_an_attachment_on_a_derived_type_and_in_a_value_object()
    {
        // Arrange
        using var context = new ShapesContext();

        // Act
        var columns = StoredFileMapping.StorageKeyColumns(context.Model);

        // Assert
        columns.Select(c => $"{c.EntityType.ClrType.Name}.{string.Join('.', c.PathToKey.Select(m => m.Name))}")
            .ShouldBe(
                [
                    "Report.File.StorageKey", "Brand.Logo.StorageKey", "Brand.Banner.StorageKey", "Contract.File.StorageKey",
                    "Shelf.Cover.Image.StorageKey",
                ],
                ignoreOrder: true,
                "The sweep of the bucket deletes each object that no column of the list names.");
    }

    [Test]
    public void a_collection_of_stored_files_stops_the_scan()
    {
        // Arrange
        using var context = new AlbumContext();

        // Act
        var act = () => StoredFileMapping.StorageKeyColumns(context.Model);

        // Assert
        act.ShouldThrow<NotSupportedException>().Message.ShouldContain("Album.Files");
    }

    [Test]
    public void a_stored_file_in_one_column_stops_the_scan()
    {
        // Arrange
        using var context = new PosterContext();

        // Act
        var act = () => StoredFileMapping.StorageKeyColumns(context.Model);

        // Assert
        act.ShouldThrow<NotSupportedException>().Message.ShouldContain("Poster.File");
    }

    private sealed class Report : IAttachment
    {
        public string Id { get; private set; } = null!;
        public string TenantId { get; private set; } = null!;
        public StoredFile File { get; private set; } = null!;
        public DateTimeOffset UploadedAt { get; private set; }
    }

    private sealed class Brand
    {
        public string Id { get; private set; } = null!;
        public StoredFile Logo { get; private set; } = null!;
        public StoredFile Banner { get; private set; } = null!;
    }

    private class Document
    {
        public string Id { get; private set; } = null!;
    }

    private sealed class Contract : Document, IAttachment
    {
        public string TenantId { get; private set; } = null!;
        public StoredFile File { get; private set; } = null!;
        public DateTimeOffset UploadedAt { get; private set; }
    }

    private sealed class Cover
    {
        public StoredFile Image { get; private set; } = null!;
    }

    private sealed class Shelf
    {
        public string Id { get; private set; } = null!;
        public Cover Cover { get; private set; } = null!;
    }

    private sealed class Poster
    {
        public string Id { get; private set; } = null!;
        public StoredFile File { get; private set; } = null!;
    }

    private sealed class Album
    {
        public string Id { get; private set; } = null!;
        public List<StoredFile> Files { get; private set; } = [];
    }

    private static DbContextOptions<TContext> OptionsOf<TContext>()
        where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options;

    private sealed class ShapesContext() : DbContext(OptionsOf<ShapesContext>())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Report>().ComplexProperty(r => r.File);
            modelBuilder.Entity<Brand>(brand =>
            {
                brand.ComplexProperty(b => b.Logo);
                brand.ComplexProperty(b => b.Banner);
            });
            modelBuilder.Entity<Document>();
            modelBuilder.Entity<Contract>().HasBaseType<Document>().ComplexProperty(c => c.File);
            modelBuilder.Entity<Shelf>().ComplexProperty(s => s.Cover, cover => cover.ComplexProperty(c => c.Image));
        }
    }

    private sealed class PosterContext() : DbContext(OptionsOf<PosterContext>())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Poster>().Property(p => p.File).HasConversion(file => file.StorageKey, _ => null!);
    }

    private sealed class AlbumContext() : DbContext(OptionsOf<AlbumContext>())
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Album>().ComplexCollection(a => a.Files, files => files.ToJson());
    }
}
