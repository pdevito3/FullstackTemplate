namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class QueryTagAnalyzerTests
{
    private const string Usings = """
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using FullstackTemplate.Server.Databases;
        using FullstackTemplate.Server.Domain;
        using FullstackTemplate.Server.Domain.Tenants;
        using FullstackTemplate.Server.Domain.Users;
        using Microsoft.EntityFrameworkCore;
        """;

    [Test]
    public async Task an_untagged_query_of_the_past_mistake_gets_fsttel001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var identifierInUse = await dbContext.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Identifier == identifier, cancellationToken);
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            """The query on 'Users' has no TagWithOperationCallSite. Put .TagWithOperationCallSite("area.operation") directly after 'Users'. See docs/backend/feature-telemetry.md.""");
        Text(diagnostic).ShouldBe("dbContext.Users");
    }

    [Test]
    public async Task a_tagged_query_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var identifierInUse = await dbContext.Users
                .TagWithOperationCallSite("users.initiate.identifier_in_use")
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Identifier == identifier, cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task an_untagged_query_on_set_gets_fsttel001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var count = await dbContext.Set<User>().CountAsync(cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("dbContext.Set<User>()");
    }

    [Test]
    public async Task a_tag_with_a_name_that_is_not_constant_has_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public static class Rows
            {
                public static async Task<List<TEntity>> LoadAsync<TEntity>(AppDbContext dbContext, string operationName, CancellationToken cancellationToken)
                    where TEntity : BaseEntity
                    => [.. await dbContext.Set<TEntity>().TagWithOperationCallSite(operationName).ToListAsync(cancellationToken)];
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("dbContext.Users.Add(user);")]
    [Arguments("await dbContext.Users.AddAsync(user, cancellationToken);")]
    [Arguments("dbContext.Users.AddRange(user);")]
    [Arguments("await dbContext.Users.AddRangeAsync([user], cancellationToken);")]
    [Arguments("dbContext.Users.Remove(user);")]
    [Arguments("dbContext.Users.RemoveRange(user);")]
    [Arguments("dbContext.Users.Attach(user);")]
    [Arguments("dbContext.Users.Update(user);")]
    [Arguments("var local = dbContext.Users.Local.Count;")]
    [Arguments("var found = dbContext.Users.Find(identifier);")]
    [Arguments("var found = await dbContext.Users.FindAsync([identifier], cancellationToken);")]
    [Arguments("var entry = dbContext.Entry(user);")]
    [Arguments("var entityType = dbContext.Set<User>().EntityType;")]
    public async Task a_use_of_the_dbset_that_is_not_a_query_has_no_diagnostics(string statement)
    {
        // Act
        var diagnostics = await RunInHandler(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("RemoveRange")]
    [Arguments("AddRange")]
    [Arguments("UpdateRange")]
    [Arguments("AttachRange")]
    public async Task an_untagged_query_passed_to_a_range_method_of_the_dbset_gets_fsttel001(string method)
    {
        // Act
        var diagnostics = await RunInHandler($"""
            dbContext.Users.{method}(dbContext.Users.Where(u => u.Identifier == identifier));
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL001");
        diagnostic.Location.SourceSpan.Start.ShouldBeGreaterThan(diagnostic.Location.SourceTree!.GetText().ToString().IndexOf(method, StringComparison.Ordinal));
    }

    [Test]
    public async Task a_tagged_query_passed_to_a_range_method_of_the_dbset_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            dbContext.Users.RemoveRange(dbContext.Users.TagWithOperationCallSite("users.remove_batch").Where(u => u.Identifier == identifier));
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_query_in_a_local_that_is_tagged_on_the_first_line_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var query = dbContext.Users
                .TagWithOperationCallSite("users.list")
                .AsNoTracking();
            query = query.Where(u => u.Identifier == identifier);
            var users = await query.ToListAsync(cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_query_in_a_local_that_is_tagged_later_gets_fsttel001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var query = dbContext.Users.AsNoTracking();
            var users = await query.TagWithOperationCallSite("users.list").ToListAsync(cancellationToken);
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL001");
    }

    [Test]
    public async Task a_dbset_in_a_local_of_type_iqueryable_gets_fsttel001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            IQueryable<User> query = dbContext.Users;
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL001");
    }

    [Test]
    public async Task a_dbset_that_an_outer_query_reads_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var tenantIds = await dbContext.Users
                .TagWithOperationCallSite("users.tenants")
                .Where(u => dbContext.Tenants.Any(t => t.Id == u.TenantId))
                .Join(dbContext.Tenants, u => u.TenantId, t => t.Id, (u, t) => t.Id)
                .ToListAsync(cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_query_in_a_local_that_only_an_outer_query_reads_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var batch = dbContext.Users.Where(u => u.Identifier == identifier).Select(u => u.Id);
            var deleted = await dbContext.Users
                .TagWithOperationCallSite("users.delete_batch")
                .Where(u => batch.Contains(u.Id))
                .ExecuteDeleteAsync(cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_helper_query_that_each_caller_tags_has_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class Directory(AppDbContext dbContext)
            {
                public Task<string?> FindAsync(string identifier, CancellationToken cancellationToken)
                    => LiveUsers()
                        .TagWithOperationCallSite("users.directory.find")
                        .Where(u => u.Identifier == identifier)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                private IQueryable<User> LiveUsers()
                    => dbContext.Users.IgnoreQueryFilters().Join(dbContext.Tenants, u => u.TenantId, t => t.Id, (u, _) => u);
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_caller_that_does_not_tag_a_helper_query_gets_fsttel003_at_the_call()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class Directory(AppDbContext dbContext)
            {
                public Task<string?> FindAsync(string identifier, CancellationToken cancellationToken)
                    => LiveUsers()
                        .Where(u => u.Identifier == identifier)
                        .Select(u => u.Id)
                        .FirstOrDefaultAsync(cancellationToken);

                private IQueryable<User> LiveUsers() => Users();

                private IQueryable<User> Users() => dbContext.Users.IgnoreQueryFilters();
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL003");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            """'LiveUsers()' returns a query with no TagWithOperationCallSite, and this caller does not tag it. Put .TagWithOperationCallSite("area.operation") directly after 'LiveUsers()'. See docs/backend/feature-telemetry.md.""");
        Text(diagnostic).ShouldBe("LiveUsers()");
    }

    [Test]
    public void only_the_report_through_a_helper_waits_for_the_whole_compilation()
    {
        // Act
        var compilationEnd = new DiagnosticAnalyzer[] { new QueryTagAnalyzer(), new HelperQueryTagAnalyzer() }
            .SelectMany(a => a.SupportedDiagnostics)
            .Where(d => d.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd))
            .Select(d => d.Id);

        // Assert
        compilationEnd.ShouldBe(["FSTTEL003"]);
    }

    [Test]
    public async Task a_conditional_of_two_queries_that_is_tagged_after_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var users = await (identifier.Length > 0 ? dbContext.Users : dbContext.Users.IgnoreQueryFilters())
                .TagWithOperationCallSite("users.list")
                .ToListAsync(cancellationToken);
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_conditional_of_two_queries_that_is_not_tagged_gets_fsttel001_on_each()
    {
        // Act
        var diagnostics = await RunInHandler("""
            var users = await (identifier.Length > 0 ? dbContext.Users : dbContext.Users.IgnoreQueryFilters()).ToListAsync(cancellationToken);
            """);

        // Assert
        diagnostics.Select(d => d.Id).ShouldBe(["FSTTEL001", "FSTTEL001"]);
    }

    [Test]
    [Arguments("private IQueryable<User> LiveUsers(bool all) => all ? dbContext.Users.IgnoreQueryFilters() : dbContext.Users;")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) => all switch { true => dbContext.Users.IgnoreQueryFilters(), false => dbContext.Users };")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) => (all ? null : dbContext.Users.AsNoTracking()) ?? dbContext.Users;")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) { var query = dbContext.Users.AsQueryable(); if (all) query = query.IgnoreQueryFilters(); return query; }")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) { IQueryable<User> query = dbContext.Users; return all ? query.IgnoreQueryFilters() : query; }")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) { IQueryable<User> query; if (all) query = dbContext.Users.IgnoreQueryFilters(); else query = dbContext.Users; return query; }")]
    public async Task a_helper_that_returns_a_query_through_a_branch_or_a_local_has_no_diagnostics_when_each_caller_tags(string helper)
    {
        // Act
        var diagnostics = await Run($$"""
            public sealed class Directory(AppDbContext dbContext)
            {
                public Task<int> CountAsync(CancellationToken cancellationToken)
                    => LiveUsers(true).TagWithOperationCallSite("users.directory.count").CountAsync(cancellationToken);

                {{helper}}
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("private IQueryable<User> LiveUsers(bool all) => all ? dbContext.Users.IgnoreQueryFilters() : dbContext.Users;")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) { var query = dbContext.Users.AsQueryable(); if (all) query = query.IgnoreQueryFilters(); return query; }")]
    [Arguments("private IQueryable<User> LiveUsers(bool all) { IQueryable<User> query; query = dbContext.Users; return query; }")]
    public async Task a_helper_that_returns_a_query_through_a_branch_or_a_local_gets_fsttel003_at_a_caller_that_does_not_tag(string helper)
    {
        // Act
        var diagnostics = await Run($$"""
            public sealed class Directory(AppDbContext dbContext)
            {
                public Task<int> CountAsync(CancellationToken cancellationToken) => LiveUsers(true).CountAsync(cancellationToken);

                {{helper}}
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL003");
        Text(diagnostic).ShouldBe("LiveUsers(true)");
    }

    [Test]
    public async Task a_query_assigned_to_a_local_that_runs_gets_fsttel001()
    {
        // Act
        var diagnostics = await RunInHandler("""
            IQueryable<User> query;
            query = dbContext.Users.Where(u => u.Identifier == identifier);
            var users = await query.ToListAsync(cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("dbContext.Users");
    }

    [Test]
    public async Task a_query_in_two_locals_that_assign_each_other_gets_fsttel001_when_it_runs()
    {
        // Act
        var diagnostics = await RunInHandler("""
            IQueryable<User> first = dbContext.Users;
            IQueryable<User> second;
            second = first;
            first = second;
            var users = await first.ToListAsync(cancellationToken);
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("dbContext.Users");
    }

    [Test]
    [Arguments("""private IEnumerable<User> LiveUsers() => dbContext.Users.Where(u => u.Id != "");""")]
    [Arguments("private IEnumerable<User> LiveUsers() { var query = dbContext.Users.AsNoTracking(); return query; }")]
    [Arguments("""private object LiveUsers() => dbContext.Users.Where(u => u.Id != "");""")]
    public async Task a_helper_that_returns_an_untagged_query_as_a_type_that_is_not_iqueryable_gets_fsttel001(string helper)
    {
        // Act
        var diagnostics = await Run($$"""
            public sealed class Directory(AppDbContext dbContext)
            {
                public object Live() => LiveUsers();

                {{helper}}
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL001");
        Text(diagnostic).ShouldBe("dbContext.Users");
    }

    [Test]
    public async Task a_query_in_a_local_that_runs_before_the_method_returns_it_gets_fsttel001()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class Directory(AppDbContext dbContext)
            {
                public IQueryable<User> LiveUsers()
                {
                    var query = dbContext.Users.IgnoreQueryFilters();
                    _ = query.Count();
                    return query;
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL001");
    }

    [Test]
    public async Task an_untagged_query_passed_to_a_method_that_tags_it_gets_fsttel001()
    {
        // Act
        var diagnostics = await Run("""
            public static class Pager
            {
                public static Task<List<T>> PageAsync<T>(IQueryable<T> query, string operationName, CancellationToken cancellationToken)
                    => query.TagWithOperationCallSite(operationName).ToListAsync(cancellationToken);
            }

            public sealed class Directory(AppDbContext dbContext)
            {
                public Task<List<User>> PageAsync(CancellationToken cancellationToken)
                    => Pager.PageAsync(dbContext.Users.Where(u => u.Id != ""), "users.page", cancellationToken);
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL001");
    }

    [Test]
    public async Task a_dbset_of_another_context_has_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class OtherContext : DbContext
            {
                public DbSet<User> Users => Set<User>();

                public Task<int> CountAsync(CancellationToken cancellationToken) => Users.CountAsync(cancellationToken);
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static string Text(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Task<ImmutableArray<Diagnostic>> RunInHandler(string body)
        => Run($$"""
            public sealed class Handler(AppDbContext dbContext)
            {
                public async Task Handle(string identifier, User user, CancellationToken cancellationToken)
                {
                    {{body}}
                }
            }
            """);

    private static async Task<ImmutableArray<Diagnostic>> Run(string source)
    {
        var references = PlatformReferences.All;
        var compilation = CSharpCompilation.Create(
            "Features",
            [CSharpSyntaxTree.ParseText($"""
                namespace App;

                {Usings}

                {source}
                """, path: "Source.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation
            .WithAnalyzers([new QueryTagAnalyzer(), new HelperQueryTagAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
