namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;

/// <summary>
/// The decision of <see cref="WriteGuard"/> for each <see cref="CommandSource"/>. A source that a new EF version adds
/// is missing from the expected maps, so the test fails until someone decides it.
/// </summary>
public class WriteGuardTests
{
    private const string WriteOfUsers = "UPDATE users SET first_name = @p WHERE id = @id";
    private const string ReadOfUsers = "SELECT first_name FROM users WHERE id = @id";
    private static readonly HashSet<string> GuardedTables = ["users", "audit_entries"];

    /// <summary>Each value once. The obsolete <c>BulkUpdate</c> has the value of <c>ExecuteDelete</c>.</summary>
    private static readonly CommandSource[] Sources = Enum.GetValues<CommandSource>().Distinct().ToArray();

    [Test]
    public void write_that_names_a_guarded_table_runs_only_from_ef_itself()
    {
        // Act
        var refused = Sources
            .ToDictionary(source => source, source => WriteGuard.Refusal(source, WriteOfUsers, false, GuardedTables) is not null);

        // Assert
        refused.ShouldBe(new Dictionary<CommandSource, bool>
        {
            [CommandSource.Unknown] = true,
            [CommandSource.LinqQuery] = false,
            [CommandSource.SaveChanges] = false,
            [CommandSource.Migrations] = false,
            [CommandSource.FromSqlQuery] = true,
            [CommandSource.ExecuteSqlRaw] = true,
            [CommandSource.ValueGenerator] = false,
            [CommandSource.Scaffolding] = false,
            [CommandSource.ExecuteDelete] = true,
            [CommandSource.ExecuteUpdate] = true,
        }, ignoreOrder: true);
    }

    [Test]
    public void raw_read_of_a_guarded_table_runs_and_other_raw_sql_runs_only_without_a_guarded_table()
    {
        // Act
        var refused = Sources
            .ToDictionary(source => source, source => WriteGuard.Refusal(source, ReadOfUsers, false, GuardedTables) is not null);

        // Assert
        refused.Where(pair => pair.Value).Select(pair => pair.Key).ShouldBe(
            [CommandSource.Unknown, CommandSource.ExecuteSqlRaw, CommandSource.ExecuteDelete, CommandSource.ExecuteUpdate],
            ignoreOrder: true);
    }

    [Test]
    public void sql_that_names_no_guarded_table_runs_from_each_source()
        => Sources
            .Where(source => WriteGuard.Refusal(source, "DELETE FROM used_dpop_proofs", false, GuardedTables) is not null)
            .ShouldBeEmpty();

    [Test]
    public void refusal_names_the_table_and_the_way_to_write_it()
        => WriteGuard.Refusal(CommandSource.ExecuteUpdate, WriteOfUsers, false, GuardedTables).ShouldBe(
            "users is audited, so write it through SaveChangesAsync or BulkInsertAsync. ExecuteUpdate, ExecuteDelete, " +
            "and raw SQL do not write audit entries. An entity type that needs no audit has [NotAudited(reason)].");

    [Test]
    public void handler_runs_only_linq_queries_even_on_a_table_with_no_audit()
    {
        // Act
        var allowed = Sources
            .Where(source => WriteGuard.Refusal(source, "SELECT 1", true, GuardedTables) is null)
            .ToList();

        // Assert
        allowed.ShouldBe([CommandSource.LinqQuery]);
        WriteGuard.Refusal(CommandSource.SaveChanges, "SELECT 1", true, GuardedTables)
            .ShouldStartWith("A domain event handler must not run SQL other than a LINQ query");
    }
}
