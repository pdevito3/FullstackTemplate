namespace FullstackTemplate.UnitTests.Databases;

using System.Globalization;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;

public class StringColumnLengthTests
{
    public sealed record CallerColumn(string RequestField, Func<string, int, string> ValueOfLength, Action<string> Write);

    // Postgres counts U+1F600 as one character, and .NET counts it as two UTF-16 units.
    private static readonly string[] Characters = ["a", "\U0001F600"];

    private static string Text(string character, int length) => string.Concat(Enumerable.Repeat(character, length));

    private static string Email(string character, int length) => Text(character, length - "@example.com".Length) + "@example.com";

    private static readonly Dictionary<string, CallerColumn> CallerColumns = new()
    {
        ["users.first_name"] = new("firstName", Text, v => TestGraph.Start(out _).AddTenant(out _).AddUser(out _, u => u.FirstName = v)),
        ["users.last_name"] = new("lastName", Text, v => TestGraph.Start(out _).AddTenant(out _).AddUser(out _, u => u.LastName = v)),
        ["users.identifier"] = new("identifier", Text, v => TestGraph.Start(out _).AddTenant(out _).AddUser(out _, u => u.Identifier = v)),
        ["users.username"] = new("username", Text, v => TestGraph.Start(out _).AddTenant(out _).AddUser(out _, u => u.Username = v)),
        ["users.email"] = new("email", Email, v => TestGraph.Start(out _).AddTenant(out _).AddUser(out _, u => u.Email = v)),
        ["tenants.name"] = new("name", Text, v => TestGraph.Start(out _).AddTenant(out _, t => t.Name = v)),
        ["agents.name"] = new("name", Text, v => TestGraph.Start(out _).AddTenant(out _).AddAgent(out _, a => a.Name = v)),
        ["agents.client_id"] = new("clientId", Text, v => TestGraph.Start(out _).AddTenant(out _).AddAgent(out _, a => a.ClientId = v)),
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> FixedOptionColumns = new()
    {
        ["users.role"] = UserRole.ListNames(),
        ["agents.mode"] = [AgentMode.Delegated().Value, AgentMode.Machine().Value],
        ["user_permissions.permission"] = Permission.GetAll().Select(p => p.Value).ToList(),
        ["agent_permissions.permission"] = Permission.GetAll().Select(p => p.Value).ToList(),
    };

    private static readonly HashSet<string> ServerWrittenColumns =
    [
        "audit_entries.action",
        "audit_entries.actor_agent_id",
        "audit_entries.actor_user_id",
        "audit_entries.entity_id",
        "audit_entries.entity_type",
        "audit_entries.kind",
        "audit_entries.request_name",
        "audit_entries.root_id",
        "audit_entries.root_type",
        "audit_entries.tenant_id",
        "audit_entries.trace_id",
    ];

    [Test]
    public void every_string_column_is_a_caller_a_fixed_option_or_a_server_column()
    {
        // Arrange
        var columns = NonIdStringColumnsWithMaxLength();

        // Act
        var unclassified = columns.Keys
            .Where(c => !CallerColumns.ContainsKey(c) && !FixedOptionColumns.ContainsKey(c) && !ServerWrittenColumns.Contains(c))
            .Order()
            .ToList();
        var stale = CallerColumns.Keys.Concat(FixedOptionColumns.Keys).Concat(ServerWrittenColumns)
            .Where(c => !columns.ContainsKey(c))
            .Order()
            .ToList();

        // Assert
        unclassified.ShouldBeEmpty("Add each column to CallerColumns with its domain write, to FixedOptionColumns, or to ServerWrittenColumns.");
        stale.ShouldBeEmpty();
    }

    public static IEnumerable<(string, string)> CallerColumnsAndCharacters =>
        CallerColumns.Keys.SelectMany(column => Characters.Select(character => (column, character)));

    [Test]
    [MethodDataSource(nameof(CallerColumnsAndCharacters))]
    public void the_domain_takes_a_value_of_the_column_length_and_refuses_a_longer_one(string column, string character)
    {
        // Arrange
        var maxLength = NonIdStringColumnsWithMaxLength()[column];
        var callerColumn = CallerColumns[column];
        var fits = callerColumn.ValueOfLength(character, maxLength);
        var tooLong = callerColumn.ValueOfLength(character, maxLength + 1);

        // Act
        var writeFits = () => callerColumn.Write(fits);
        var writeTooLong = () => callerColumn.Write(tooLong);

        // Assert
        writeFits.ShouldNotThrow();
        var exception = writeTooLong.ShouldThrow<ValidationException>();
        exception.Property.ShouldBe(callerColumn.RequestField);
        exception.Message.ShouldContain(maxLength.ToString(CultureInfo.InvariantCulture));
    }

    public static IEnumerable<string> CallerColumnNames => CallerColumns.Keys;

    [Test]
    [MethodDataSource(nameof(CallerColumnNames))]
    public void the_domain_refuses_a_too_long_value_of_lone_surrogates_with_a_validation_error(string column)
    {
        // Arrange
        var maxLength = NonIdStringColumnsWithMaxLength()[column];
        var callerColumn = CallerColumns[column];
        var tooLong = callerColumn.ValueOfLength("\uD800", maxLength + 1);

        // Act
        var writeTooLong = () => callerColumn.Write(tooLong);

        // Assert
        writeTooLong.ShouldThrow<ValidationException>().Property.ShouldBe(callerColumn.RequestField);
    }

    [Test]
    public void each_option_of_a_fixed_option_column_fits_the_column()
    {
        // Arrange
        var columns = NonIdStringColumnsWithMaxLength();

        // Act
        var tooLong = FixedOptionColumns
            .SelectMany(c => c.Value.Where(o => o.Length > columns[c.Key]).Select(o => $"{c.Key}: {o}"))
            .ToList();

        // Assert
        tooLong.ShouldBeEmpty();
    }

    private static Dictionary<string, int> NonIdStringColumnsWithMaxLength()
    {
        using var context = new AppDbContextFactory().CreateDbContext([]);
        var model = context.GetService<IDesignTimeModel>().Model;

        return model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .SelectMany(e => e.GetFlattenedProperties())
            .Where(p => (p.GetTypeMapping().Converter?.ProviderClrType ?? p.ClrType) == typeof(string) && p.GetMaxLength() is not null && !p.IsKey() && !p.IsForeignKey())
            .ToDictionary(
                p => $"{p.DeclaringType.ContainingEntityType.GetTableName()}.{p.GetColumnName()}",
                p => p.GetMaxLength()!.Value);
    }
}
