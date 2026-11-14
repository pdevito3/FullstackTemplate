namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Attachments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

/// <summary>
/// The sweep of the bucket looks up each page of keys in each attachment table. EF Core 10 cannot index a column of a
/// value object, so only a migration adds the index, and only this test sees a table that has none.
/// </summary>
public class AttachmentStorageKeyIndexTests : TestBase
{
    private const string IndexedTablesSql =
        """
        SELECT table_class.relname
        FROM pg_index
        JOIN pg_class table_class ON table_class.oid = pg_index.indrelid
        JOIN pg_namespace ON pg_namespace.oid = table_class.relnamespace
        JOIN pg_attribute ON pg_attribute.attrelid = table_class.oid AND pg_attribute.attnum = pg_index.indkey[0]
        WHERE pg_namespace.nspname = current_schema()
          AND pg_attribute.attname = 'storage_key'
        """;

    [Test]
    public async Task each_attachment_table_has_an_index_that_starts_with_its_storage_key()
    {
        // Arrange
        var attachmentTables = new TestingServiceScope().GetService<AppDbContext>().Model.GetEntityTypes()
            .Where(e => e.BaseType is null && e.ClrType.IsAssignableTo(typeof(IAttachment)))
            .Select(e => e.GetTableName())
            .ToList();
        await using var connection = new NpgsqlConnection(TestFixture.UnpooledConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(IndexedTablesSql, connection);

        // Act
        var indexedTables = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                indexedTables.Add(reader.GetString(0));
        }

        // Assert
        attachmentTables.Except(indexedTables).ShouldBeEmpty();
    }
}
