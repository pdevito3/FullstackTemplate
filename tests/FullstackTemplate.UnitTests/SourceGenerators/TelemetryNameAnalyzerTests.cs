namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class TelemetryNameAnalyzerTests
{
    private const string Usings = """
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Diagnostics.Metrics;
        using System.Linq;
        using FullstackTemplate.Server.Databases;
        using FullstackTemplate.Server.Domain.Users;
        using FullstackTemplate.Server.Resources;
        using Serilog;
        """;

    [Test]
    public async Task a_span_name_with_an_upper_case_letter_gets_fsttel004()
    {
        // Act
        var diagnostics = await RunInMethod("""using var activity = Telemetry.Trace([], "Invoice.render");""");

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            "The span name 'Invoice.render' breaks the name rules. Use lower case parts in snake_case joined by dots, as area.operation, for example invoice.render. See the Names section of docs/backend/feature-telemetry.md.");
        Text(diagnostic).ShouldBe("""
            "Invoice.render"
            """);
    }

    [Test]
    [Arguments("""using var activity = Telemetry.Trace([], "invoice");""", "span name", "invoice")]
    [Arguments("""using var activity = Telemetry.Trace([], "invoice-render");""", "span name", "invoice-render")]
    [Arguments("""using var trace = Telemetry.TraceRoot([], "database.Migrate");""", "span name", "database.Migrate")]
    [Arguments("""Telemetry.AddEvent([], "lockAcquired");""", "event name", "lockAcquired")]
    [Arguments("""Activity.Current?.AddEvent(new ActivityEvent("cache miss"));""", "event name", "cache miss")]
    [Arguments("""var users = dbContext.Users.TagWithOperationCallSite("GetUsers").ToList();""", "query name", "GetUsers")]
    [Arguments("""Activity.Current?.SetTag("UserId", id);""", "tag key", "UserId")]
    [Arguments("""Activity.Current?.SetTag("user_id", id);""", "tag key", "user_id")]
    [Arguments("""Activity.Current?.SetTag("app.userId", id);""", "tag key", "app.userId")]
    [Arguments("""using var activity = Telemetry.Trace([("app.Lock.mode", id)], "lock.acquire");""", "tag key", "app.Lock.mode")]
    [Arguments("""Telemetry.AddEvent([("count", 1)], "race.lost");""", "tag key", "count")]
    [Arguments("""using var activity = Telemetry.Trace(new (string, object?)[] { ("Raw Key", 1) }, "lock.acquire");""", "tag key", "Raw Key")]
    [Arguments("""var counter = Telemetry.Meter.CreateCounter<long>("users.role_changed");""", "metric name", "users.role_changed")]
    [Arguments("""var counter = Telemetry.Meter.CreateHistogram<double>("app.users");""", "metric name", "app.users")]
    [Arguments("""var counter = Telemetry.Meter.CreateUpDownCounter<long>("app.Users.active");""", "metric name", "app.Users.active")]
    [Arguments("""var gauge = Telemetry.Meter.CreateGauge<long>("app.users.role_changed.count");""", "metric name", "app.users.role_changed.count")]
    [Arguments("""Logger.Information("Added user {userId}", id);""", "log property", "userId")]
    [Arguments("""Logger.Warning("Skipped {row_count} rows", 1);""", "log property", "row_count")]
    [Arguments("""Logger.Error("Could not load {0}", id);""", "log property", "0")]
    [Arguments("""Logger.Information("Added {user.Id}", id);""", "log property", "user.Id")]
    [Arguments("""var counter = Telemetry.Meter.CreateCounter<long>("app.request");""", "metric name", "app.request")]
    [Arguments("""Activity.Current?.SetTag("user.id", id);""", "tag key", "user.id")]
    [Arguments("""Activity.Current?.SetTag("user.role", id);""", "tag key", "user.role")]
    [Arguments("""Activity.Current?.SetTag("enduser.id", id);""", "tag key", "enduser.id")]
    [Arguments("""Activity.Current?.SetTag("session.id", id);""", "tag key", "session.id")]
    public async Task a_name_that_breaks_the_names_table_gets_fsttel004(string statement, string kind, string name)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL004");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldStartWith($"The {kind} '{name}' breaks the name rules.");
    }

    [Test]
    [Arguments("""using var activity = Telemetry.Trace([("app.bulk_insert.row_count", 1)], "database.bulk_insert");""")]
    [Arguments("""using var trace = Telemetry.TraceRoot([("app.db_context", id)], "dpop.replay_cache.cleanup");""")]
    [Arguments("""Telemetry.AddEvent([("app.race.run", 1)], "race.lost");""")]
    [Arguments("""Activity.Current?.AddEvent(new ActivityEvent("migration.lock_acquired"));""")]
    [Arguments("""var users = dbContext.Users.TagWithOperationCallSite("users.get_by_id").ToList();""")]
    [Arguments("""Activity.Current?.SetTag("app.user_id", id);""")]
    [Arguments("""Activity.Current?.SetTag("app.tenant_lookup.removed_count", 1);""")]
    [Arguments("""Activity.Current?.SetTag("app.caller.tenant_id", id);""")]
    [Arguments("""Activity.Current?.SetTag("error.type", id);""")]
    [Arguments("""Activity.Current?.SetTag("code.line.number", 1);""")]
    [Arguments("""Activity.Current?.SetTag("gen_ai.tool.name", id);""")]
    [Arguments("""Activity.Current?.SetTag("server.address", id);""")]
    [Arguments("""Activity.Current?.SetTag("server.port", id);""")]
    [Arguments("""Activity.Current?.SetTag("otel.status_code", id);""")]
    [Arguments("""Activity.Current?.SetTag("messaging.system", id);""")]
    [Arguments("""Activity.Current?.SetTag("network.peer.address", id);""")]
    [Arguments("""Activity.Current?.SetTag("rpc.method", id);""")]
    [Arguments("""Activity.Current?.SetTag("peer.service", id);""")]
    [Arguments("""using var activity = Telemetry.Trace([("app.kind", Describe(("Raw Key", 1)))], "lock.acquire"); static string Describe((string, int) item) => item.Item1;""")]
    [Arguments("""var counter = Telemetry.Meter.CreateCounter<long>("app.users.role_changed", unit: "{user}");""")]
    [Arguments("""var counter = Telemetry.Meter.CreateCounter<long>("app.errors");""")]
    [Arguments("""Logger.Information("Added user {UserId} to {TenantId}", id, id);""")]
    [Arguments("""Logger.Information("Deleted {@DeletedCounts}", new { Users = 1 });""")]
    [Arguments("""Logger.Information("Read {$Mode}", id);""")]
    [Arguments("""Logger.Information("Deleted {Count:N0} rows in {Elapsed,8:0.00} s", 1, 2.0);""")]
    [Arguments("""Logger.Information("Sent {{ \"a\": 1 }} and { not a property } to {UserId}", id);""")]
    public async Task a_name_that_fits_the_names_table_has_no_diagnostics(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""using var activity = Telemetry.Trace([(name, 1)], name);""")]
    [Arguments("""using var trace = Telemetry.TraceRoot([], name);""")]
    [Arguments("""Telemetry.AddEvent([], name);""")]
    [Arguments("""Activity.Current?.AddEvent(new ActivityEvent(name));""")]
    [Arguments("""var users = dbContext.Users.TagWithOperationCallSite(name).ToList();""")]
    [Arguments("""Activity.Current?.SetTag(name, id);""")]
    [Arguments("""var counter = Telemetry.Meter.CreateCounter<long>(name);""")]
    [Arguments("""Logger.Information(name, id);""")]
    public async Task a_name_that_is_not_constant_has_no_diagnostics(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""(string, object?)[] tags = [("Raw Key", 1)]; using var activity = Telemetry.Trace(tags, "lock.acquire");""")]
    [Arguments("""var tags = new (string, object?)[] { ("Raw Key", 1) }; using var activity = Telemetry.Trace(tags, "lock.acquire");""")]
    [Arguments("""(string, object?)[] tags = [("Raw Key", 1)]; using var trace = Telemetry.TraceRoot(tags, "lock.acquire");""")]
    [Arguments("""(string, object?)[] tags = [("Raw Key", 1)]; Telemetry.AddEvent(tags, "lock.acquired");""")]
    [Arguments("""(string, object?)[] tags = [("app.lock.mode", 1)]; tags = [("Raw Key", 1)]; Telemetry.AddEvent(tags, "lock.acquired");""")]
    [Arguments("""(string, object?)[] tags; tags = [("Raw Key", 1)]; Telemetry.AddEvent(tags, "lock.acquired");""")]
    [Arguments("""(string, object?)[] tags = [("Raw Key", 1)]; System.Action write = () => Telemetry.AddEvent(tags, "lock.acquired");""")]
    public async Task a_tags_array_in_a_local_with_a_bad_key_gets_fsttel004(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("\"Raw Key\"");
    }

    [Test]
    [Arguments("""(string, object?)[] tags = [("app.lock.mode", 1)]; using var activity = Telemetry.Trace(tags, "lock.acquire");""")]
    [Arguments("""(string, object?)[] tags = [(name, 1)]; Telemetry.AddEvent(tags, "lock.acquired");""")]
    [Arguments("""var tags = Tags(); Telemetry.AddEvent(tags, "lock.acquired");""")]
    [Arguments("""(string, object?)[] tags = [("Raw Key", 1)]; System.Console.WriteLine(tags.Length);""")]
    public async Task a_tags_array_in_a_local_with_no_bad_key_has_no_diagnostics(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""var tags = new TagList { { "UserId", id } };""")]
    [Arguments("""TagList tags = new() { { "UserId", id } };""")]
    [Arguments("""var tags = new TagList(); tags.Add("UserId", id);""")]
    [Arguments("""var tags = new TagList(); tags.Add(new KeyValuePair<string, object?>("UserId", id));""")]
    [Arguments("""var tags = new TagList(); tags.Add(new("UserId", id));""")]
    [Arguments("""var tags = new TagList(); tags.Insert(0, new KeyValuePair<string, object?>("UserId", id));""")]
    [Arguments("""var tags = new TagList(); tags[0] = new KeyValuePair<string, object?>("UserId", id);""")]
    [Arguments("""var tags = new TagList { new KeyValuePair<string, object?>("UserId", id) };""")]
    [Arguments("""TagList tags = [new("UserId", id)];""")]
    [Arguments("""TagList tags = [new KeyValuePair<string, object?>("UserId", id)];""")]
    [Arguments("""var tags = new TagList([new KeyValuePair<string, object?>("UserId", id)]);""")]
    [Arguments("""var tags = new TagList(new[] { new KeyValuePair<string, object?>("UserId", id) });""")]
    [Arguments("""Telemetry.Errors.Add(1, new TagList { { "UserId", id } });""")]
    [Arguments("""var tags = new ActivityTagsCollection { { "UserId", id } };""")]
    [Arguments("""var tags = new ActivityTagsCollection { ["UserId"] = id };""")]
    [Arguments("""var tags = new ActivityTagsCollection(); tags["UserId"] = id;""")]
    [Arguments("""var tags = new ActivityTagsCollection(); tags.Add("UserId", id);""")]
    [Arguments("""var tags = new ActivityTagsCollection(); tags.Add(new KeyValuePair<string, object?>("UserId", id));""")]
    [Arguments("""var tags = new ActivityTagsCollection { new KeyValuePair<string, object?>("UserId", id) };""")]
    [Arguments("""ActivityTagsCollection tags = [new("UserId", id)];""")]
    [Arguments("""var tags = new ActivityTagsCollection([new("UserId", id)]);""")]
    [Arguments("""var tags = new ActivityTagsCollection(new[] { new KeyValuePair<string, object?>("UserId", id) });""")]
    [Arguments("""Activity.Current?.AddEvent(new ActivityEvent("cache.missed", tags: new ActivityTagsCollection { { "UserId", id } }));""")]
    public async Task a_bad_key_in_a_tag_list_or_an_activity_tags_collection_gets_fsttel004(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldStartWith("The tag key 'UserId' breaks the name rules.");
        Text(diagnostic).ShouldBe("\"UserId\"");
    }

    [Test]
    [Arguments("""var tags = new TagList { { "app.request.name", id } };""")]
    [Arguments("""var tags = new TagList(); tags.Add("error.type", id);""")]
    [Arguments("""var tags = new TagList { new KeyValuePair<string, object?>("exception.message", id) };""")]
    [Arguments("""TagList tags = [new("app.user_id", id)];""")]
    [Arguments("""var tags = new TagList(); tags.Add(name, id);""")]
    [Arguments("""var tags = new TagList([new KeyValuePair<string, object?>(name, id)]);""")]
    [Arguments("""var tags = new ActivityTagsCollection { { "app.user_id", id } };""")]
    [Arguments("""var tags = new ActivityTagsCollection { ["error.type"] = id };""")]
    [Arguments("""var tags = new ActivityTagsCollection(); tags[name] = id;""")]
    [Arguments("""var tags = new ActivityTagsCollection(); var found = tags["UserId"]; tags.Remove("UserId"); tags.ContainsKey("UserId");""")]
    [Arguments("""var tags = new Dictionary<string, object?> { { "UserId", id } };""")]
    [Arguments("""var tags = new Dictionary<string, object?> { ["UserId"] = id };""")]
    [Arguments("""var tags = new List<KeyValuePair<string, object?>> { new("UserId", id) };""")]
    [Arguments("""var tag = new KeyValuePair<string, object?>("UserId", id);""")]
    public async Task a_tag_list_or_an_activity_tags_collection_with_no_bad_key_has_no_diagnostics(string statement)
    {
        // Act
        var diagnostics = await RunInMethod(statement);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_constant_field_that_breaks_the_names_table_gets_fsttel004()
    {
        // Act
        var diagnostics = await Run("""
            public static class Tags
            {
                public const string UserId = "UserId";

                public static void Run(string id) => Activity.Current?.SetTag(UserId, id);
            }
            """);

        // Assert
        Text(diagnostics.ShouldHaveSingleItem()).ShouldBe("UserId");
    }

    private static string Text(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Task<ImmutableArray<Diagnostic>> RunInMethod(string statement)
        => Run($$"""
            public static class Sample
            {
                private static readonly ILogger Logger = Log.ForContext(typeof(Sample));

                public static void Run(AppDbContext dbContext, string name, string id)
                {
                    {{statement}}
                }

                private static (string, object?)[] Tags() => [];
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
            .WithAnalyzers([new TelemetryNameAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
