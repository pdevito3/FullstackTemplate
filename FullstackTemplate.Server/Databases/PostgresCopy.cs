namespace FullstackTemplate.Server.Databases;

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

/// <summary>
/// Writes the rows of new entities with binary COPY, on the connection and in the transaction of the context. The
/// columns, the store types, and the value converters come from the EF model through <see cref="EntityColumn"/>.
/// </summary>
internal static class PostgresCopy
{
    /// <summary>
    /// Writes each table with one COPY, in the order given. Returns the id of the transaction, which is the
    /// <c>xmin</c>, so the <c>Version</c>, of each row that it wrote.
    /// </summary>
    [SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs",
        Justification = "AppDbContext.BulkInsertAsync writes the audit entries of these rows in the same transaction.")]
    public static async Task<uint> WriteAsync(
        DatabaseFacade database, IEnumerable<IGrouping<IEntityType, EntityEntry>> tables, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)database.GetDbConnection();
        var sql = database.GetService<ISqlGenerationHelper>();
        foreach (var rows in tables)
        {
            if (rows.Key.GetTableMappings().Skip(1).Any())
                throw new NotSupportedException(
                    $"BulkInsertAsync writes the rows of a type to one table, but {rows.Key.ClrType.Name} maps to more than one table, as a derived type of a TPT hierarchy does. Save it with SaveChangesAsync, or map the hierarchy with TPH or TPC.");

            var columns = EntityColumn.Of(rows.Key);
            var storeTypes = columns.Select(c => c.Property.GetRelationalTypeMapping().StoreType).ToList();
            var columnNames = string.Join(", ", columns.Select(c => sql.DelimitIdentifier(c.Property.GetColumnName())));
            await using var importer = await connection.BeginBinaryImportAsync(
                $"COPY {sql.DelimitIdentifier(rows.Key.GetTableName()!, rows.Key.GetSchema())} ({columnNames}) FROM STDIN (FORMAT BINARY)",
                cancellationToken);

            foreach (var row in rows)
            {
                await importer.StartRowAsync(cancellationToken);
                for (var i = 0; i < columns.Count; i++)
                {
                    if (columns[i].ProviderValueOf(row, original: false) is { } value)
                        await importer.WriteAsync(value, storeTypes[i], cancellationToken);
                    else
                        await importer.WriteNullAsync(cancellationToken);
                }
            }

            await importer.CompleteAsync(cancellationToken);
        }

        // Each row that a transaction inserts has the id of the transaction as its xmin.
        await using var command = new NpgsqlCommand("SELECT pg_current_xact_id()::xid", connection);
        return (uint)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
