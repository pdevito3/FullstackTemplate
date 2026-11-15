namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.EntityConfigurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

/// <summary>
/// The sweep of the bucket looks up each page of keys in each storage key column. EF Core 10 cannot index a column of a
/// value object, so only a migration adds the index, and only this test sees a column that has none.
/// </summary>
public class StorageKeyIndexTests : TestBase
{
    private const string FirstColumnsOfIndexesSql =
        """
        SELECT table_class.relname, pg_attribute.attname
        FROM pg_index
        JOIN pg_class table_class ON table_class.oid = pg_index.indrelid
        JOIN pg_namespace ON pg_namespace.oid = table_class.relnamespace
        JOIN pg_attribute ON pg_attribute.attrelid = table_class.oid AND pg_attribute.attnum = pg_index.indkey[0]
        WHERE pg_namespace.nspname = current_schema()
        """;

    [Test]
    public async Task each_storage_key_column_has_an_index_that_starts_with_it()
    {
        // Arrange
        var storageKeyColumns = StoredFileMapping.StorageKeyColumns(new TestingServiceScope().GetService<AppDbContext>().Model)
            .Select(c => $"{c.EntityType.GetTableName()}.{c.StorageKey.GetColumnName()}")
            .ToList();
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(FirstColumnsOfIndexesSql, connection);

        // Act
        var indexedColumns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                indexedColumns.Add($"{reader.GetString(0)}.{reader.GetString(1)}");
        }

        // Assert
        storageKeyColumns.Except(indexedColumns).ShouldBeEmpty();
    }
}
