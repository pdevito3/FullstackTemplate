namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

/// <summary>
/// Each failing case is a message that an app shipped and a review found: a permission code in a 403 (CarePoint 112),
/// a status name in a guard message (CarePoint 32), and a raw ID in a 404 (Fleet 76).
/// </summary>
public class UserMessageAnalyzerTests
{
    private const string Fix = "Name it as the screen names it, for example \"You cannot manage users.\" See docs/messages.md.";

    private const string Domain = """
        namespace Ardalis.SmartEnum
        {
            public abstract class SmartEnum<TEnum>(string name, int value)
            {
                public string Name { get; } = name;
                public int Value { get; } = value;
            }
        }

        namespace App
        {
            using System;

            public class ValidationException(string property, string message) : Exception(message)
            {
                public static void Must(bool condition, string property, string message)
                {
                    if (!condition)
                        throw new ValidationException(property, message);
                }
            }

            public class ForbiddenAccessException(string message) : Exception(message);

            public class NotFoundException(string message) : Exception(message);

            public interface IFixedNames { }

            public class Permission(string value) : IFixedNames
            {
                public string Value { get; } = value;
                public string Words { get; } = "manage users";
            }

            public class AppointmentStatus
            {
                private StatusEnum _status = StatusEnum.Scheduled;

                public string Value => _status.Name;

                private sealed class StatusEnum(string name, int value) : Ardalis.SmartEnum.SmartEnum<StatusEnum>(name, value)
                {
                    public static readonly StatusEnum Scheduled = new("Scheduled", 0);
                }
            }

            public enum Mode { Delegated, Machine }

            public class TimeZoneId(string value)
            {
                public string Value { get; } = value;
                public override string ToString() => Value;
            }

            public static class Indexes
            {
                public static void ForRequestField(string field, string message) { }
            }
        }
        """;

    [Test]
    public async Task a_permission_code_in_the_text_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public static class Guard
                {
                    public static void Deny() => throw new ForbiddenAccessException("You need manage_users.");
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTMSG001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'manage_users'");
    }

    [Test]
    public async Task a_permission_code_that_helpers_build_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Caller
                {
                    public void Require(Permission permission) => throw new ForbiddenAccessException(DenialFor(permission));

                    private string DenialFor(Permission permission) => this switch
                    {
                        _ => UserLacks(permission),
                    };

                    private static string UserLacks(Permission permission) => $"You need the {permission.Value} permission to do this.";
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTMSG001");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'permission.Value'");
        diagnostic.Location.GetLineSpan().StartLinePosition.Line.ShouldBe(11);
    }

    [Test]
    public async Task a_status_name_from_a_smart_enum_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Appointment
                {
                    public AppointmentStatus Status { get; } = new();

                    public void Cancel()
                        => ValidationException.Must(false, "status", $"An appointment that is {Status.Value} cannot be cancelled.");
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTMSG001");
    }

    [Test]
    public async Task an_enum_value_and_a_type_name_get_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public static class Messages
                {
                    public static void ForMode(Mode mode) => throw new ValidationException("mode", $"A {mode} agent cannot do this.");

                    public static T Found<T>(T? entity) where T : class
                        => entity ?? throw new NotFoundException($"{typeof(T).Name} was not found.");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.Id).ShouldBe(["FSTMSG001", "FSTMSG001"]);
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldContain(m => m.Contains("the enum value 'mode'"));
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldContain(m => m.Contains("the type name 'typeof(T).Name'"));
    }

    [Test]
    public async Task a_pascal_case_name_in_a_message_constant_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public static class Appointments
                {
                    public const string LockedMessage = "A CheckedIn appointment cannot be moved.";
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldContain("'CheckedIn'");
    }

    [Test]
    public async Task an_id_in_the_text_gets_fstmsg002()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public static class Defects
                {
                    public static void Missing(string defectId)
                        => throw new NotFoundException($"No defect with the ID '{defectId}' is on this asset.");
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTMSG002");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'defectId'");
    }

    [Test]
    public async Task a_message_that_goes_through_a_helper_parameter_is_checked()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Asset
                {
                    public string Id { get; } = "ast_1";

                    public void Retire() => GuardActive($"Asset {Id} is retired.");

                    private static void GuardActive(string text) => ValidationException.Must(false, "status", text);
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTMSG002");
    }

    [Test]
    public async Task a_code_that_reaches_a_hole_through_a_local_a_helper_or_a_string_method_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Appointment
                {
                    public AppointmentStatus Status { get; } = new();

                    public void Cancel()
                    {
                        var status = Status.Value;
                        ValidationException.Must(false, "status", $"An appointment that is {status} cannot be cancelled.");
                    }

                    public void Deny(Permission permission) => throw new ForbiddenAccessException(Lacks(permission.Value));

                    public void Lower(Permission permission)
                        => throw new ForbiddenAccessException($"You need {permission.Value.ToLowerInvariant()}.");

                    public void Name(Permission permission) => throw new ForbiddenAccessException($"You need {permission}.");

                    public void Field() => throw new ValidationException("status", $"Set {nameof(Status)} first.");

                    private static string Lacks(string code) => $"You need the {code} permission.";
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the stored name 'Status.Value'. " + Fix,
            "A user message shows the stored name 'permission.Value'. " + Fix,
            "A user message shows the stored name 'permission.Value'. " + Fix,
            "A user message shows the stored name 'permission'. " + Fix,
            "A user message shows the code name 'nameof(Status)'. " + Fix,
        ]);
    }

    [Test]
    public async Task an_id_in_a_concatenation_a_format_or_a_type_whose_name_ends_in_id_gets_fstmsg002()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Asset
                {
                    public string Id { get; } = "ast_1";

                    public string ClientId { get; } = "fleet-sync";

                    public void Retire() => throw new ValidationException("status", "Asset " + Id + " is retired.");

                    public void Deny() => throw new ForbiddenAccessException(string.Format("The agent {0} cannot do this.", ClientId));

                    public void Zone(TimeZoneId zone) => throw new ValidationException("zone", $"The clinic is in {zone}.");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG002", "A user message shows the ID 'Id'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
            ("FSTMSG002", "A user message shows the ID 'ClientId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
            ("FSTMSG002", "A user message shows the ID 'zone'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
        ]);
    }

    [Test]
    public async Task a_message_parameter_of_a_method_that_is_not_an_exception_is_checked()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public static class Configuration
                {
                    public static void Configure() => Indexes.ForRequestField("name", "This NameInUse value is taken.");
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().GetMessage(CultureInfo.InvariantCulture).ShouldContain("'NameInUse'");
    }

    [Test]
    public async Task a_message_that_is_only_a_code_an_id_or_a_type_name_gets_a_diagnostic()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Agent
                {
                    public string ClientId { get; } = "fleet-sync";

                    public void Deny(Permission permission) => throw new ForbiddenAccessException(permission.Value);

                    public void Missing() => throw new NotFoundException(ClientId);

                    public static void Missing<T>() => throw new NotFoundException(typeof(T).FullName!);
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG001", "A user message shows the stored name 'permission.Value'. " + Fix),
            ("FSTMSG002", "A user message shows the ID 'ClientId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
            ("FSTMSG001", "A user message shows the type name 'typeof(T).FullName'. " + Fix),
        ]);
    }

    [Test]
    public async Task a_helper_argument_is_checked_only_in_the_call_that_builds_a_message()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Orders
                {
                    private static string Quote(string text) => $"'{text}'";

                    private static void Log(string text) { }

                    public void Rename(string name) => throw new ValidationException("name", $"The name {Quote(name)} is taken.");

                    public void Trace(string orderId) => Log(Quote(orderId));

                    public void Find(string orderId) => throw new NotFoundException($"The order {Quote(orderId)} was not found.");
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Location.GetLineSpan().StartLinePosition.Line.ShouldBe(12);
    }

    [Test]
    public async Task words_for_the_screen_and_developer_messages_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System;
                using System.Linq;

                public class Caller
                {
                    public const string NameInUseMessage = "A tenant with this name already exists.";

                    public void Require(Permission permission, int maxLength)
                        => throw new ForbiddenAccessException($"You cannot {permission.Words}. Use at most {maxLength} characters.");

                    public void Level(Mode mode) => throw new ValidationException("mode", $"Use level {(int)mode} or higher.");

                    public static string Describe(Type type, string entityId, string[] names)
                    {
                        var codes = string.Join(", ", names.Select(n => n + "_code"));
                        throw new InvalidOperationException($"{type.Name} '{entityId}' has the codes {codes}, so BulkInsertAsync stops.");
                    }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> Run(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Messages",
            [CSharpSyntaxTree.ParseText(Domain, path: "Domain.cs"), CSharpSyntaxTree.ParseText(source, path: "Source.cs")],
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
            .WithAnalyzers([new UserMessageAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return [.. diagnostics.OrderBy(d => d.Location.SourceSpan.Start)];
    }
}
