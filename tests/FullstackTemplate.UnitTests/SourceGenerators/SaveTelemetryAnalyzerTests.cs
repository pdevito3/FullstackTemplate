namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class SaveTelemetryAnalyzerTests
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using FullstackTemplate.Server.Databases;
        using FullstackTemplate.Server.Domain;
        using FullstackTemplate.Server.Domain.Tenants;
        using FullstackTemplate.Server.Domain.Users;
        using FullstackTemplate.Server.Resources;
        using MediatR;
        using Microsoft.EntityFrameworkCore;
        using Serilog;
        """;

    private const string Load = """
        var tenant = await dbContext.Tenants
            .TagWithOperationCallSite("tenants.update.load")
            .GetById(request.Id, cancellationToken);
        """;

    private const string Tag = """Activity.Current?.SetTag("app.tenant_id", request.Id);""";

    private const string Save = "await dbContext.SaveChangesAsync(cancellationToken);";

    private const string StateChangeLog = """_logger.Here().Information("Updated tenant {TenantId}", tenant.Id);""";

    [Test]
    public async Task a_command_handler_of_the_past_mistake_gets_fsttel002()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Load, Save);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTTEL002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldBe(
            "The handler 'App.Handler' saves changes with no state change log and no ID tag. "
            + "Add a _logger.Here().Information log with a message template after the save, "
            + "for example _logger.Here().Information(\"Updated order {OrderId}\", order.Id), "
            + "and an Activity.Current?.SetTag(\"app.<entity>_id\", ...) tag as soon as the handler knows the ID, "
            + "for example Activity.Current?.SetTag(\"app.order_id\", order.Id). "
            + "See docs/backend/feature-telemetry.md.");
        Text(diagnostic).ShouldBe("Handler");
    }

    [Test]
    public async Task a_command_handler_with_the_log_and_the_tag_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Tag, Load, Save, StateChangeLog);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_command_handler_with_no_log_gets_fsttel002_for_the_log()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Tag, Load, Save);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldStartWith(
            "The handler 'App.Handler' saves changes with no state change log. Add a _logger.Here().Information log");
    }

    [Test]
    public async Task a_command_handler_with_no_tag_gets_fsttel002_for_the_tag()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Load, Save, StateChangeLog);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldStartWith(
            "The handler 'App.Handler' saves changes with no ID tag. Add an Activity.Current?.SetTag(");
    }

    [Test]
    public async Task a_save_in_a_lambda_gets_fsttel002()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Load, "await Run(async () => await dbContext.SaveChangesAsync(cancellationToken));");

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL002");
    }

    [Test]
    public async Task the_log_and_the_tag_in_another_member_of_the_handler_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public sealed record Command(string Id) : IRequest;

            public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
            {
                private readonly ILogger _logger = Log.ForContext<Handler>();

                public Task Handle(Command request, CancellationToken cancellationToken)
                    => SaveAsync(request.Id, cancellationToken);

                private async Task SaveAsync(string id, CancellationToken cancellationToken)
                {
                    await Task.Run(async () => await dbContext.SaveChangesAsync(cancellationToken), cancellationToken);
                    TagAndLog(id);
                }

                private void TagAndLog(string id)
                {
                    Activity.Current?.SetTag("app.tenant_id", id);
                    _logger.Here().Information("Updated tenant {TenantId}", id);
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("""_logger.Information("Updated tenant {TenantId}", tenant.Id);""")]
    [Arguments("""_logger.Here().Information($"Updated tenant {tenant.Id}");""")]
    [Arguments("""_logger.Here().Warning("Updated tenant {TenantId}", tenant.Id);""")]
    public async Task a_log_that_is_not_the_state_change_log_of_the_doc_gets_fsttel002(string log)
    {
        // Act
        var diagnostics = await RunInCommandHandler(Tag, Load, Save, log);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldContain("no state change log");
    }

    [Test]
    [Arguments("""Activity.Current?.SetTag("app.tenant.name", request.Id);""")]
    [Arguments("""Activity.Current?.SetTag("tenant_id", request.Id);""")]
    [Arguments("""new Activity("child").SetTag("app.tenant_id", request.Id);""")]
    public async Task a_tag_that_is_not_an_id_on_the_feature_span_gets_fsttel002(string tag)
    {
        // Act
        var diagnostics = await RunInCommandHandler(tag, Load, Save, StateChangeLog);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldContain("no ID tag");
    }

    [Test]
    public async Task a_synchronous_save_gets_fsttel002()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Load, "dbContext.SaveChanges();");

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTTEL002");
    }

    [Test]
    public async Task a_tag_on_a_local_that_holds_the_feature_span_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInCommandHandler(
            "var activity = Activity.Current;",
            """activity?.SetTag("app.tenant_id", request.Id);""",
            Load,
            Save,
            StateChangeLog);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_tag_on_a_local_that_holds_another_span_gets_fsttel002()
    {
        // Act
        var diagnostics = await RunInCommandHandler(
            """var activity = new Activity("child");""",
            """activity.SetTag("app.tenant_id", request.Id);""",
            Load,
            Save,
            StateChangeLog);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldContain("no ID tag");
    }

    [Test]
    public async Task a_query_handler_that_does_not_save_has_no_diagnostics()
    {
        // Act
        var diagnostics = await RunInCommandHandler(Load);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_class_that_is_not_a_request_handler_has_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            public sealed class Seeder(AppDbContext dbContext)
            {
                public Task SaveAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static string Text(Diagnostic diagnostic)
        => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private static Task<ImmutableArray<Diagnostic>> RunInCommandHandler(params string[] statements)
        => Run($$"""
            public sealed record Command(string Id) : IRequest;

            public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Command>
            {
                private readonly ILogger _logger = Log.ForContext<Handler>();

                public async Task Handle(Command request, CancellationToken cancellationToken)
                {
                    {{string.Join("\n", statements)}}
                }

                private static Task Run(Func<Task> work) => work();
            }
            """);

    private static async Task<ImmutableArray<Diagnostic>> Run(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Features",
            [CSharpSyntaxTree.ParseText($"namespace App;\n\n{Usings}\n\n{source}", path: "Source.cs")],
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
            .WithAnalyzers([new SaveTelemetryAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
