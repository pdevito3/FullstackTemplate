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

                public static System.Collections.Generic.IReadOnlyCollection<TEnum> List { get; } = [];

                public static bool TryFromName(string name, bool ignoreCase, out TEnum result)
                {
                    result = default!;
                    return false;
                }
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
    public async Task an_mcp_exception_and_the_text_of_an_error_tool_result_are_user_messages()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using ModelContextProtocol;
                using ModelContextProtocol.Protocol;

                public static class Tools
                {
                    public static string Close(string workOrderId)
                        => throw new McpException($"Work order {workOrderId} needs manage_users.");

                    public static CallToolResult Deny()
                        => new() { IsError = true, Content = [new TextContentBlock { Text = "You need read_users." }] };

                    public static CallToolResult Read()
                        => new() { Content = [new TextContentBlock { Text = "read_users, manage_users" }] };
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG002", "A user message shows the ID 'workOrderId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
            ("FSTMSG001", "A user message shows the code name 'manage_users'. " + Fix),
            ("FSTMSG001", "A user message shows the code name 'read_users'. " + Fix),
        ]);
    }

    [Test]
    public async Task the_text_of_a_problem_or_an_error_result_is_a_user_message()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using Microsoft.AspNetCore.Http;

                public static class Endpoints
                {
                    public static IResult Locked() => TypedResults.Problem(detail: "The order is locked_out.");

                    public static IResult Titled() => Results.Problem(title: "OrderLocked");

                    public static IResult Rejected() => TypedResults.BadRequest("Use read_users.");

                    public static IResult Changed(string orderId) => TypedResults.Conflict($"Order {orderId} changed.");

                    public static IResult Read() => TypedResults.Ok("read_users");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG001", "A user message shows the code name 'locked_out'. " + Fix),
            ("FSTMSG001", "A user message shows the code name 'OrderLocked'. " + Fix),
            ("FSTMSG001", "A user message shows the code name 'read_users'. " + Fix),
            ("FSTMSG002", "A user message shows the ID 'orderId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
        ]);
    }

    [Test]
    public async Task a_message_in_an_auto_property_initializer_is_checked()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Order
                {
                    public static string LockedMessage { get; } = "The order is locked_out.";

                    public static string Closed { get; } = "The order is ClosedOut.";

                    public void Close() => throw new ValidationException("status", Closed);
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the code name 'locked_out'. " + Fix,
            "A user message shows the code name 'ClosedOut'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_list_of_enum_values_or_names_in_a_message_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System;
                using System.Collections.Generic;

                public static class Modes
                {
                    public static void Values() => throw new ValidationException("mode", $"Use {string.Join(", ", Enum.GetValues<Mode>())}.");

                    public static void Names() => throw new ValidationException("mode", $"Use {string.Join(", ", Enum.GetNames<Mode>())}.");

                    public static void Grants(IReadOnlyList<Permission> grants)
                        => throw new ForbiddenAccessException($"You hold {string.Join(", ", grants)}.");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the enum value 'Enum.GetValues<Mode>()'. " + Fix,
            "A user message shows the enum value 'Enum.GetNames<Mode>()'. " + Fix,
            "A user message shows the stored name 'grants'. " + Fix,
        ]);
    }

    [Test]
    public async Task an_exception_of_the_project_is_a_user_message_whatever_its_parameter_is_named()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class LockedException(string reason) : System.Exception(reason);

                public class ClosedException : System.Exception
                {
                    public ClosedException(string why) : base(why) { }
                }

                public static class Orders
                {
                    public static void Lock(string orderId) => throw new LockedException($"Order {orderId} is locked.");

                    public static void Close() => throw new ClosedException("The order is ClosedOut.");

                    public static void Broken() => throw new System.InvalidOperationException("The OrderModel has no key.");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG002", "A user message shows the ID 'orderId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
            ("FSTMSG001", "A user message shows the code name 'ClosedOut'. " + Fix),
        ]);
    }

    [Test]
    public async Task a_with_message_function_is_checked_and_a_placeholder_is_not_a_code_name()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FluentValidation;

                public record Rename(string OrderId, string Name);

                public class RenameValidator : AbstractValidator<Rename>
                {
                    public RenameValidator()
                    {
                        RuleFor(x => x.Name).NotEmpty().WithMessage("{PropertyName} is required.");
                        RuleFor(x => x.Name).MaximumLength(3).WithMessage(x => $"Order {x.OrderId} has a name that is too long.");
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => (d.Id, d.GetMessage(CultureInfo.InvariantCulture))).ShouldBe([
            ("FSTMSG002", "A user message shows the ID 'x.OrderId'. Name the record as the screen names it, or leave it out. See docs/messages.md."),
        ]);
    }

    [Test]
    public async Task the_value_that_a_parse_rejects_is_not_traced_to_the_values_that_it_accepts()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System;

                public class Grant : IFixedNames
                {
                    private const string RequestField = "grant";

                    private GrantEnum _grant;

                    public Grant(string value)
                    {
                        _grant = Parse(value, RequestField);
                    }

                    public string Value => _grant.Name;

                    public static Grant ManageUsers() => new(GrantEnum.ManageUsers.Name);

                    public static Grant ReadUsers() => new(Names.ReadUsers);

                    private static GrantEnum Parse(string value, string requestField)
                        => GrantEnum.TryFromName(value, true, out var parsed)
                            ? parsed
                            : throw new ValidationException(requestField, $"Invalid permission: {value}");

                    public static class Names
                    {
                        public const string ReadUsers = "read_users";
                    }

                    private sealed class GrantEnum(string name, int value) : Ardalis.SmartEnum.SmartEnum<GrantEnum>(name, value)
                    {
                        public static readonly GrantEnum ManageUsers = new("manage_users", 0);
                        public static readonly GrantEnum ReadUsers = new(Names.ReadUsers, 1);
                    }
                }

                // The shape of DefectSeverity in Fleet
                public sealed class DefectSeverity
                {
                    public const string DefaultRequestField = "severity";

                    private SeverityEnum _severity;

                    public DefectSeverity(string? value, string requestField = DefaultRequestField)
                    {
                        _severity = Parse(value!, requestField);
                    }

                    public static DefectSeverity Critical() => new(SeverityEnum.Critical.Name);

                    public static DefectSeverity Minor() => new(SeverityEnum.Minor.Name);

                    private static SeverityEnum Parse(string value, string requestField) =>
                        SeverityEnum.TryFromName(value, true, out var parsed)
                            ? parsed
                            : throw new ValidationException(requestField, $"Invalid defect severity: {value}. Use Critical or Minor.");

                    private sealed class SeverityEnum(string name, int value) : Ardalis.SmartEnum.SmartEnum<SeverityEnum>(name, value)
                    {
                        public static readonly SeverityEnum Critical = new("Critical", 0);
                        public static readonly SeverityEnum Minor = new("Minor", 1);
                    }
                }

                public static class Modes
                {
                    public static Mode Machine() => Parse(nameof(Mode.Machine));

                    private static Mode Parse(string value)
                    {
                        if (!Enum.TryParse<Mode>(value, out var mode))
                            throw new ValidationException("mode", $"Invalid mode: {value}");

                        return mode;
                    }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_parse_message_still_fails_on_its_own_text_and_a_check_that_is_not_a_parse_is_traced()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System.Collections.Generic;

                public class Result
                {
                    public Result(string value, Mode mode)
                    {
                        _ = Parse(value, mode);
                    }

                    public static Result Pass() => new("Pass", Mode.Machine);

                    private static Mode Parse(string value, Mode mode)
                        => System.Enum.TryParse<Mode>(value, out var parsed)
                            ? parsed
                            : throw new ValidationException("result", $"Invalid result for {mode}: {value}. Use Pass or NotApplicable.");
                }

                public class Caller(HashSet<string> codes)
                {
                    public const string ManageUsers = "manage_users";

                    public void Manage() => Require(ManageUsers);

                    private void Require(string code)
                    {
                        if (!codes.Contains(code))
                            throw new ForbiddenAccessException($"You need {code}.");
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the enum value 'mode'. " + Fix,
            "A user message shows the code name 'NotApplicable'. " + Fix,
            "A user message shows the code name 'manage_users'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_value_that_a_parse_accepted_is_traced()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Visit
                {
                    public static void Start() => MoveTo(StatusEnum.CheckedIn.Name);

                    private static void MoveTo(string status)
                    {
                        if (StatusEnum.TryFromName(status, true, out var parsed) && parsed == StatusEnum.CheckedIn)
                            throw new ValidationException("status", $"The visit is already {status}.");
                    }

                    private sealed class StatusEnum(string name, int value) : Ardalis.SmartEnum.SmartEnum<StatusEnum>(name, value)
                    {
                        public static readonly StatusEnum CheckedIn = new("CheckedIn", 0);
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the stored name 'StatusEnum.CheckedIn.Name'. " + Fix,
        ]);
    }

    [Test]
    public async Task problem_details_validation_failures_and_grpc_status_details_are_user_messages()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FluentValidation;
                using FluentValidation.Results;

                public record Rename(string Name);

                public class RenameValidator : AbstractValidator<Rename>
                {
                    public RenameValidator()
                    {
                        RuleFor(x => x.Name).Custom((name, context) => context.AddFailure("name", "Use the OrderName field."));
                    }
                }

                public static class Problems
                {
                    public static Microsoft.AspNetCore.Mvc.ProblemDetails Locked() => new() { Detail = "The order is locked_out." };

                    public static ValidationFailure Failure() => new("status", "Use CheckedIn.");

                    public static Grpc.Core.Status Missing(string orderId) => new(Grpc.Core.StatusCode.NotFound, $"Order {orderId} was not found.");

                    public static string Placeholder() => throw new ValidationException("status", "Status CheckedIn} is closed.");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the code name 'OrderName'. " + Fix,
            "A user message shows the code name 'locked_out'. " + Fix,
            "A user message shows the code name 'CheckedIn'. " + Fix,
            "A user message shows the ID 'orderId'. Name the record as the screen names it, or leave it out. See docs/messages.md.",
            "A user message shows the code name 'CheckedIn'. " + Fix,
        ]);
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

    [Test]
    public async Task a_list_of_stored_names_from_list_names_or_a_select_gets_fstmsg001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System.Collections.Generic;
                using System.Linq;

                // The shape of WorkOrderStatus in Fleet and ChargeKind in CarePoint
                public class Grade : IFixedNames
                {
                    public static List<string> ListNames() => GradeEnum.List.OrderBy(x => x.Value).Select(x => x.Name).ToList();

                    public static void Pick() => throw new ValidationException("grade", $"Use one of {string.Join(", ", ListNames())}.");

                    public static void PickName() => throw new ValidationException("grade", $"Use one of {string.Join(", ", GradeEnum.List.Select(k => k.Name))}.");

                    public static void PickValue() => throw new ValidationException("grade", $"Use one of {string.Join(", ", GradeEnum.List.Select(k => k.Value))}.");

                    public static void PickFromHelper() => throw new ValidationException("grade", $"Use one of {string.Join(", ", Names())}.");

                    public static void PickWords()
                        => throw new ValidationException("grade", $"Use one of {string.Join(", ", GradeEnum.List.Where(k => k.Name != "").Select(k => k.Label))}. There are {GradeEnum.List.Count()}.");

                    private static IEnumerable<string> Names() => GradeEnum.List.Select(x => x.Name);

                    private sealed class GradeEnum(string name, int value, string label) : Ardalis.SmartEnum.SmartEnum<GradeEnum>(name, value)
                    {
                        public static readonly GradeEnum FirstGrade = new("FirstGrade", 0, "first grade");

                        public string Label { get; } = label;
                    }
                }

                // A ListNames that a loop builds
                public class Level : IFixedNames
                {
                    public static List<string> ListNames()
                    {
                        var names = new List<string>();
                        foreach (var level in LevelEnum.List)
                            names.Add(level.Name);
                        return names;
                    }

                    public static void Pick() => throw new ValidationException("level", $"Use one of {string.Join(", ", Level.ListNames())}.");

                    private sealed class LevelEnum(string name, int value) : Ardalis.SmartEnum.SmartEnum<LevelEnum>(name, value);
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the stored name 'ListNames()'. " + Fix,
            "A user message shows the stored name 'GradeEnum.List.Select(k => k.Name)'. " + Fix,
            "A user message shows the stored name 'GradeEnum.List.Select(k => k.Value)'. " + Fix,
            "A user message shows the stored name 'Names()'. " + Fix,
            "A user message shows the stored name 'Level.ListNames()'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_try_method_that_is_not_a_parse_is_traced()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Stage : IFixedNames
                {
                    public static void Start() => new Stage().MoveTo("CheckedIn");

                    public bool TryMoveTo(string target) => target.Length > 0;

                    public void MoveTo(string target)
                    {
                        if (!TryMoveTo(target))
                            throw new ValidationException("stage", $"Cannot move to {target}.");
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the code name 'CheckedIn'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_guard_clause_parse_and_an_is_false_parse_are_not_traced()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System;

                public static class Modes
                {
                    public static Mode Machine() => Parse(nameof(Mode.Machine));

                    public static Mode Delegated() => ParseOrFail(nameof(Mode.Delegated));

                    public static void Log() => Check(nameof(Mode.Machine));

                    private static Mode Parse(string value)
                    {
                        if (Enum.TryParse<Mode>(value, out var mode))
                            return mode;

                        throw new ValidationException("mode", $"Invalid mode: {value}");
                    }

                    private static Mode ParseOrFail(string value)
                    {
                        if (Enum.TryParse<Mode>(value, out var mode) is false)
                            throw new ValidationException("mode", $"Invalid mode: {value}");

                        return mode;
                    }

                    // The if does not leave the method, so the throw also runs after a parse that accepts the value
                    private static void Check(string value)
                    {
                        if (Enum.TryParse<Mode>(value, out _))
                            Console.WriteLine(value);

                        throw new ValidationException("mode", $"Invalid mode: {value}");
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the code name 'nameof(Mode.Machine)'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_reassigned_parameter_and_the_text_of_an_error_status_are_checked()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using Microsoft.AspNetCore.Http;

                public class Order
                {
                    public Mode Kind { get; } = Mode.Machine;

                    public void Close(string reason)
                    {
                        reason = $"The order is {Kind}.";
                        throw new ValidationException("status", reason);
                    }

                    public void Reopen() => throw new ValidationException("status", Describe("closed"));

                    private string Describe(string state)
                    {
                        state = $"The order is {Kind}.";
                        return state;
                    }

                    public static IResult Locked() => Results.Text("The order is locked_out.", statusCode: 400);

                    public static IResult Changed() => TypedResults.Text("Use read_users.", statusCode: StatusCodes.Status409Conflict);

                    public static IResult Read() => Results.Text("read_users");
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the enum value 'Kind'. " + Fix,
            "A user message shows the enum value 'Kind'. " + Fix,
            "A user message shows the code name 'locked_out'. " + Fix,
            "A user message shows the code name 'read_users'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_generic_list_names_a_chain_over_a_local_and_a_coalesce_assignment_are_checked()
    {
        // Act
        var diagnostics = await Run("""
            namespace Other
            {
                using System.Collections.Generic;

                public interface IFixedNames
                {
                    static abstract List<string> ListNames();
                }

                public static class Choices
                {
                    public static string Of<T>() where T : IFixedNames => string.Join(", ", T.ListNames());
                }
            }

            namespace App
            {
                using System.Linq;

                public class Kind : Other.IFixedNames
                {
                    private static readonly string[] Names = KindEnum.List.Select(x => x.Name).ToArray();

                    public static System.Collections.Generic.List<string> ListNames() => [.. Names];

                    public static void Generic() => throw new ValidationException("kind", $"Use one of {Other.Choices.Of<Kind>()}.");

                    public static void Ordered() => throw new ValidationException("kind", $"Use one of {string.Join(", ", Names.Order())}.");

                    public static void Trimmed() => throw new ValidationException("kind", $"Use one of {string.Join(", ", KindEnum.List.Select(x => x.Name).Select(n => n.Trim()))}.");

                    public static void Longest() => throw new ValidationException("kind", $"The longest is {KindEnum.List.Max(k => k.Label)}.");

                    public static void Deny(Permission permission, string? message = null)
                    {
                        message ??= $"You cannot {permission.Value}.";
                        throw new ForbiddenAccessException(message);
                    }

                    private sealed class KindEnum(string name, int value, string label) : Ardalis.SmartEnum.SmartEnum<KindEnum>(name, value)
                    {
                        public static readonly KindEnum LateCancellation = new("LateCancellation", 0, "late cancellation");

                        public string Label { get; } = label;
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the stored name 'T.ListNames()'. " + Fix,
            "A user message shows the stored name 'KindEnum.List.Select(x => x.Name)'. " + Fix,
            "A user message shows the stored name 'KindEnum.List.Select(x => x.Name).Select(n => n.Trim())'. " + Fix,
            "A user message shows the stored name 'permission.Value'. " + Fix,
        ]);
    }

    [Test]
    public async Task a_yield_return_guard_and_a_throw_in_a_local_function_are_not_parses()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using System;
                using System.Collections.Generic;

                public static class Modes
                {
                    public static void Machine() => _ = Parse(nameof(Mode.Machine));

                    public static void Delegated() => _ = Check(nameof(Mode.Delegated), true);

                    private static IEnumerable<Mode> Parse(string value)
                    {
                        if (Enum.TryParse<Mode>(value, out var mode))
                            yield return mode;

                        throw new ValidationException("mode", $"Invalid mode: {value}");
                    }

                    private static string Check(string value, bool strict)
                    {
                        if (strict)
                            Fail();
                        if (Enum.TryParse<Mode>(value, out _))
                            return "ok";
                        void Fail() => throw new ValidationException("mode", $"Unknown mode {value}");
                        return "";
                    }
                }
            }
            """);

        // Assert
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).ShouldBe([
            "A user message shows the code name 'nameof(Mode.Machine)'. " + Fix,
            "A user message shows the code name 'nameof(Mode.Delegated)'. " + Fix,
        ]);
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
