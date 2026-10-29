namespace FullstackTemplate.UnitTests.Docs;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql;
using Shouldly;

// The examples of docs/backend/entity-configurations.md that the Server does not have.

public sealed class Revision
{
    public int Id { get; private set; }
    public int DocumentId { get; private set; }
    public bool IsCurrent { get; private set; }
    public bool IsDeleted { get; private set; }
}

public sealed class RevisionConfiguration : IEntityTypeConfiguration<Revision>
{
    public void Configure(EntityTypeBuilder<Revision> builder)
    {
        builder.HasIndex(e => e.DocumentId)
            .IsUnique()
            .HasFilter("is_current AND is_deleted = false")
            .ForStaleVersion();
    }
}

public sealed class AddUserEmailIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "ix_users_email",
            table: "users",
            column: "email",
            unique: true,
            filter: "is_deleted = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_users_email",
            table: "users");
    }
}

public sealed class AddAppointmentOverlapConstraint : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
        migrationBuilder.Sql("""
            ALTER TABLE appointments ADD CONSTRAINT ex_appointments_provider_time
            EXCLUDE USING gist (provider_id WITH =, tstzrange(starts_at, ends_at) WITH &&)
            WHERE (is_deleted = false);
            """);
    }
}

public class EntityConfigurationExampleTests
{
    [Test]
    public void a_save_that_the_current_revision_index_stops_is_a_stale_version()
    {
        // Arrange
        using var context = new RevisionContext();
        var index = context.Model.FindEntityType(typeof(Revision))!.GetIndexes().ShouldHaveSingleItem();
        var failure = new DbUpdateException("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                constraintName: index.GetDatabaseName()));

        // Act
        var conflict = DatabaseConflict.From(failure, context.Model);

        // Assert
        conflict.ShouldBeOfType<StaleVersionException>();
    }

    [Test]
    public void the_email_index_migration_creates_the_filtered_unique_index_and_drops_it()
    {
        // Arrange
        var migration = new AddUserEmailIndex();

        // Act
        var up = migration.UpOperations.ShouldHaveSingleItem().ShouldBeOfType<CreateIndexOperation>();
        var down = migration.DownOperations.ShouldHaveSingleItem().ShouldBeOfType<DropIndexOperation>();

        // Assert
        up.IsUnique.ShouldBeTrue();
        up.Filter.ShouldBe("is_deleted = false");
        down.Name.ShouldBe(up.Name);
    }

    private sealed class RevisionContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=unused");

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
            => configurationBuilder.MapConstraintErrors();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ApplyConfiguration(new RevisionConfiguration());
    }
}
