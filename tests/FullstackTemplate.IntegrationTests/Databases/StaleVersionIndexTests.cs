namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

/// <summary>
/// The schema has no stale-version index, so the test uses a model of its own on a temporary table. A temporary
/// table lives only in the session of its connection, so other tests do not see it, and the context keeps the
/// connection open for the test.
/// </summary>
public class StaleVersionIndexTests : TestBase
{
    private const string CurrentRevisionIndex = "ix_revisions_document_id_current";

    [Test]
    public async Task second_current_revision_of_a_document_is_a_stale_version()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();
        await using (var command = new NpgsqlCommand(
            $"""
            CREATE TEMP TABLE revisions (id uuid PRIMARY KEY, document_id uuid NOT NULL, is_current boolean NOT NULL);
            CREATE UNIQUE INDEX {CurrentRevisionIndex} ON revisions (document_id) WHERE is_current;
            """, connection))
            await command.ExecuteNonQueryAsync();

        await using var dbContext = new RevisionContext(connection);
        var documentId = Guid.NewGuid();
        dbContext.Add(new Revision { DocumentId = documentId, IsCurrent = true });
        await dbContext.SaveChangesAsync();
        dbContext.Add(new Revision { DocumentId = documentId, IsCurrent = true });

        // Act
        var failure = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        // Assert
        failure.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe(CurrentRevisionIndex);
        var exception = DatabaseConflict.From(failure, dbContext.Model).ShouldBeOfType<StaleVersionException>();
        exception.InnerException.ShouldBeSameAs(failure);
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    private sealed class RevisionContext(NpgsqlConnection connection) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql(connection);

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.MapConstraintErrors();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var revision = modelBuilder.Entity<Revision>();
            revision.ToTable("revisions");
            revision.Property(r => r.Id).HasColumnName("id");
            revision.Property(r => r.DocumentId).HasColumnName("document_id");
            revision.Property(r => r.IsCurrent).HasColumnName("is_current");
            revision.HasIndex(r => r.DocumentId)
                .IsUnique()
                .HasFilter("is_current")
                .HasDatabaseName(CurrentRevisionIndex)
                .ForStaleVersion();
        }
    }

    private sealed class Revision
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public bool IsCurrent { get; set; }
    }
}
