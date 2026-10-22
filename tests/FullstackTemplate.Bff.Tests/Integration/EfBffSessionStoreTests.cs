namespace FullstackTemplate.Bff.Tests.Integration;

using System.Data.Common;
using FullstackTemplate.Bff.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Shouldly;

/// <summary>
/// The SQL of the EF Core adapter on the <c>bffdb</c> of the test run.
/// </summary>
public class EfBffSessionStoreTests
{
    [Test]
    [NotInParallel(BffSessionStoreContractTests.EndedRowDeletes)]
    public async Task each_cleanup_delete_takes_its_batch_from_the_start_of_an_expiry_index()
    {
        var capture = new NonQueryCapture();
        var options = new DbContextOptionsBuilder<BffDbContext>();
        BffSessionExtensions.ConfigureBffDb(options, BffApp.Database.ConnectionString);
        options.AddInterceptors(capture);
        await using var dbContext = new BffDbContext(options.Options);
        var store = new EfBffSessionStore(dbContext, TimeProvider.System);

        // A time in the past, so that the deletes change no row of another test
        await store.DeleteEndedAsync(new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), 10, CancellationToken.None);

        capture.Commands.ShouldNotBeEmpty();
        await using var connection = new NpgsqlConnection(BffApp.Database.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        // The test table is small, so the planner would read all of it and sort it. Make the full reads and the
        // sort cost much, as on a large table, so that the plan shows if the batch can come from an index in order.
        await using (var planner = new NpgsqlCommand(
            "SET LOCAL enable_seqscan = off; SET LOCAL enable_bitmapscan = off; SET LOCAL enable_sort = off", connection, transaction))
            await planner.ExecuteNonQueryAsync();

        foreach (var command in capture.Commands)
        {
            var plan = await ExplainAsync(connection, transaction, command);
            plan.ShouldContain("Limit", customMessage: plan);
            plan.ShouldNotContain("Sort", customMessage: plan);
            plan.ShouldMatch(@"Index Scan using ix_sessions_(absolute_)?expires on sessions[\s\S]*Index Cond: \((absolute_)?expires <=",
                customMessage: plan);
        }
    }

    private static async Task<string> ExplainAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, DbCommand command)
    {
        await using var explain = new NpgsqlCommand($"EXPLAIN {command.CommandText}", connection, transaction);
        foreach (NpgsqlParameter parameter in command.Parameters)
            explain.Parameters.Add(parameter.Clone());

        var lines = new List<string>();
        await using var reader = await explain.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            lines.Add(reader.GetString(0));
        return string.Join('\n', lines);
    }

    /// <summary>
    /// Keeps a copy of each non-query command, such as the <c>DELETE</c> of <c>ExecuteDeleteAsync</c>.
    /// </summary>
    private sealed class NonQueryCapture : DbCommandInterceptor
    {
        public List<DbCommand> Commands { get; } = [];

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(Copy(command));
            return ValueTask.FromResult(result);
        }

        private static NpgsqlCommand Copy(DbCommand command)
        {
            var copy = new NpgsqlCommand(command.CommandText);
            foreach (NpgsqlParameter parameter in command.Parameters)
                copy.Parameters.Add(parameter.Clone());
            return copy;
        }
    }
}
