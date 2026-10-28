namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class ConstraintErrorsTests : TestBase
{
    [Test]
    public async Task each_unique_index_and_exclusion_constraint_in_the_schema_has_an_error()
    {
        // Arrange
        var dbContext = new TestingServiceScope().GetService<AppDbContext>();

        // Act
        var constraints = await dbContext.Database.SqlQueryRaw<string>(
            """
            SELECT index_class.relname AS "Value"
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
            """).ToListAsync();

        // Assert
        constraints.ShouldNotBeEmpty();
        constraints.Where(c => !ConstraintConflictException.ConstraintErrors.ContainsKey(c)).ShouldBeEmpty();
    }
}
