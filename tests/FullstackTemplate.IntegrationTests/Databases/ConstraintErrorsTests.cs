namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shouldly;

/// <summary>
/// Each unique index and each exclusion constraint of the schema declares one <see cref="ConstraintError"/>: a request
/// field, or a stale version. The model build refuses an index with both, so an index with neither is the gap to find.
/// </summary>
public class ConstraintErrorsTests : TestBase
{
    private const string ConstraintsSql =
        """
        SELECT index_class.relname
        FROM pg_index
        JOIN pg_class index_class ON index_class.oid = pg_index.indexrelid
        JOIN pg_class table_class ON table_class.oid = pg_index.indrelid
        JOIN pg_namespace ON pg_namespace.oid = table_class.relnamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_index.indisunique
          AND NOT pg_index.indisprimary
          AND table_class.relname <> '__EFMigrationsHistory'
        UNION
        SELECT pg_constraint.conname
        FROM pg_constraint
        JOIN pg_namespace ON pg_namespace.oid = pg_constraint.connamespace
        WHERE pg_namespace.nspname = current_schema()
          AND pg_constraint.contype = 'x'
        """;

    [Test]
    public async Task each_unique_index_and_exclusion_constraint_in_the_schema_declares_a_request_field_or_a_stale_version()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();

        // Act
        var constraints = await ConstraintsAsync(connection, transaction: null);

        // Assert
        constraints.ShouldNotBeEmpty();
        Undeclared(constraints, AppModel()).ShouldBeEmpty();
    }

    [Test]
    public async Task unique_index_that_declares_neither_is_undeclared()
    {
        // Arrange
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();

        // Postgres DDL is transactional, so other tests never see the table, and the rollback drops it
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand(
            """
            CREATE TABLE constraint_guard_probes (id uuid PRIMARY KEY, code text NOT NULL);
            CREATE UNIQUE INDEX ix_constraint_guard_probes_code ON constraint_guard_probes (code);
            """, connection, transaction))
            await command.ExecuteNonQueryAsync();

        // Act
        var constraints = await ConstraintsAsync(connection, transaction);

        // Assert
        Undeclared(constraints, AppModel()).ShouldBe(["ix_constraint_guard_probes_code"]);
    }

    private static IModel AppModel() => new TestingServiceScope().GetService<AppDbContext>().Model;

    private static IEnumerable<string> Undeclared(IEnumerable<string> constraints, IModel model)
    {
        var errors = ConstraintErrors.Of(model);
        return constraints.Where(constraint => !errors.ContainsKey(constraint));
    }

    private static async Task<List<string>> ConstraintsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        await using var command = new NpgsqlCommand(ConstraintsSql, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var constraints = new List<string>();
        while (await reader.ReadAsync())
            constraints.Add(reader.GetString(0));
        return constraints;
    }
}
