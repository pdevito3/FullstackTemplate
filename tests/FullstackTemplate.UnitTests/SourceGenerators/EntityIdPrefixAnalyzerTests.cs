namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Collections.Immutable;
using System.Globalization;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

public class EntityIdPrefixAnalyzerTests
{
    private const string BaseEntity = """
        namespace FullstackTemplate.Server.Domain
        {
            public abstract class BaseEntity
            {
                protected BaseEntity(string idPrefix) { }
            }
        }
        """;

    [Test]
    public void the_longest_prefix_makes_an_id_of_the_maximum_length()
    {
        // Assert
        EntityIdPrefixAnalyzer.MaxPrefixLength.ShouldBe(EntityId.MaxPrefixLength);
        (EntityIdPrefixAnalyzer.MaxPrefixLength + "_".Length + 32).ShouldBe(EntityId.MaxLength);
        EntityId.New(new string('a', EntityIdPrefixAnalyzer.MaxPrefixLength)).Length.ShouldBe(EntityId.MaxLength);
    }

    [Test]
    public async Task valid_entities_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class Tenant : BaseEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }

                public class User : BaseEntity
                {
                    public const string IdPrefix = "abcdefghijklmnopq";
                    protected User() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task an_entity_without_a_prefix_gets_fstid001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected Tenant() : base("ten") { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID001");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("App.Tenant");
    }

    [Test]
    public async Task a_prefix_that_is_not_a_public_string_constant_gets_fstid001()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    private const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTID001");
    }

    [Test]
    [Arguments("")]
    [Arguments("Ten")]
    [Arguments("ten_a")]
    [Arguments("te1")]
    [Arguments("tén")]
    [Arguments("abcdefghijklmnopqr")]
    public async Task an_invalid_prefix_gets_fstid002(string prefix)
    {
        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                public class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    public const string IdPrefix = "{{prefix}}";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID002");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("App.Tenant");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain($"'{prefix}'");
    }

    [Test]
    public async Task a_duplicate_prefix_gets_fstid003_on_each_entity()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }

                public class Member : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected Member() : base(IdPrefix) { }
                }

                public class Tenant : BaseEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(d => d.Id == "FSTID003" && d.Severity == DiagnosticSeverity.Error);
        diagnostics.ShouldContain(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.User'") && d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Member'"));
        diagnostics.Select(d => d.Location.GetLineSpan().StartLinePosition.Line).Distinct().Count().ShouldBe(2);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task a_partial_entity_without_a_prefix_gets_one_fstid001(bool baseListOnBothParts)
    {
        // Arrange
        var secondBaseList = baseListOnBothParts ? ": FullstackTemplate.Server.Domain.BaseEntity" : "";

        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                public partial class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected Tenant() : base("ten") { }
                }

                public partial class Tenant {{secondBaseList}}
                {
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTID001");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task a_partial_entity_with_an_invalid_prefix_gets_one_fstid002(bool baseListOnBothParts)
    {
        // Arrange
        var secondBaseList = baseListOnBothParts ? ": FullstackTemplate.Server.Domain.BaseEntity" : "";

        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                public partial class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    public const string IdPrefix = "Ten";
                    protected Tenant() : base(IdPrefix) { }
                }

                public partial class Tenant {{secondBaseList}}
                {
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTID002");
    }

    [Test]
    public async Task constructors_that_pass_the_own_prefix_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class Tenant : BaseEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(Tenant.IdPrefix) { }
                    protected Tenant(string name) : this() { }
                }

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(App.User.IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("User.IdPrefix")]
    [Arguments("\"ten\"")]
    [Arguments("Other")]
    public async Task a_constructor_that_does_not_pass_the_own_prefix_gets_fstid004(string argument)
    {
        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }

                public class Tenant : BaseEntity
                {
                    public const string IdPrefix = "ten";
                    private const string Other = "ten";
                    protected Tenant() : base({{argument}}) { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID004");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Tenant'");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("its own IdPrefix");
    }

    [Test]
    public async Task a_constructor_in_another_part_of_a_partial_entity_is_checked()
    {
        // Act
        var diagnostics = await Run(
            """
            namespace App
            {
                public partial class Tenant : FullstackTemplate.Server.Domain.BaseEntity
                {
                    public const string IdPrefix = "ten";
                }

                public class User : FullstackTemplate.Server.Domain.BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }
            }
            """,
            """
            namespace App
            {
                public partial class Tenant
                {
                    protected Tenant() : base(User.IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("FSTID004");
    }

    [Test]
    public async Task an_abstract_base_that_does_not_pass_its_parameter_gets_fstid004()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected AuditedEntity() : base("aud") { }
                }

                public class Tenant : AuditedEntity
                {
                    public const string IdPrefix = "ten";
                }
            }
            """);

        // Assert
        diagnostics.Length.ShouldBe(2);
        diagnostics.ShouldAllBe(d => d.Id == "FSTID004");
        diagnostics.ShouldContain(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.AuditedEntity'") && d.GetMessage(CultureInfo.InvariantCulture).Contains("its own idPrefix parameter"));
        diagnostics.ShouldContain(d => d.GetMessage(CultureInfo.InvariantCulture).Contains("'App.Tenant'") && d.GetMessage(CultureInfo.InvariantCulture).Contains("its own IdPrefix"));
    }

    [Test]
    public async Task primary_constructors_that_pass_the_right_prefix_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public abstract class AuditedEntity(string idPrefix) : BaseEntity(idPrefix);

                public class Order() : AuditedEntity(IdPrefix)
                {
                    public const string IdPrefix = "ord";
                }

                public class User() : BaseEntity(IdPrefix)
                {
                    public const string IdPrefix = "usr";
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("\"ord\"")]
    [Arguments("User.IdPrefix")]
    public async Task a_primary_constructor_that_does_not_pass_the_own_prefix_gets_fstid004(string argument)
    {
        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }

                public class Order() : BaseEntity({{argument}})
                {
                    public const string IdPrefix = "ord";
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID004");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Order'");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("its own IdPrefix");
    }

    [Test]
    [Arguments("\"aud\"")]
    [Arguments("App.User.IdPrefix")]
    public async Task an_abstract_base_with_a_primary_constructor_that_does_not_pass_its_parameter_gets_fstid004(string argument)
    {
        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }

                public abstract class AuditedEntity(string idPrefix) : BaseEntity({{argument}});

                public class Tenant : AuditedEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID004");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.AuditedEntity'");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("its own idPrefix parameter");
    }

    [Test]
    public async Task constructors_that_pass_the_own_prefix_among_more_arguments_have_no_diagnostics()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected AuditedEntity(string idPrefix, string createdBy) : base(idPrefix) { }
                }

                public class Tenant : AuditedEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix, "system") { }
                }

                public class Order() : AuditedEntity(IdPrefix, "system")
                {
                    public const string IdPrefix = "ord";
                }

                public class User : AuditedEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(createdBy: "system", idPrefix: IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    [Arguments("(string idPrefix, string createdBy) : base(createdBy)", "base(IdPrefix, \"system\")", "App.AuditedEntity")]
    [Arguments("(string idPrefix, string createdBy) : base(idPrefix)", "base(\"system\", IdPrefix)", "App.Tenant")]
    [Arguments("(string prefix) : base(prefix)", "base(IdPrefix)", "App.AuditedEntity", "App.Tenant")]
    public async Task a_prefix_that_does_not_bind_to_the_idprefix_parameter_gets_fstid004(
        string auditedConstructor, string tenantInitializer, params string[] entities)
    {
        // Act
        var diagnostics = await Run($$"""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected AuditedEntity{{auditedConstructor}} { }
                }

                public class Tenant : AuditedEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : {{tenantInitializer}} { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldAllBe(d => d.Id == "FSTID004");
        diagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture).Split('\'')[1]).Order().ShouldBe(entities);
    }

    [Test]
    public async Task a_primary_constructor_without_base_arguments_gets_fstid004()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected AuditedEntity(string idPrefix) : base(idPrefix) { }
                    protected AuditedEntity() : this("aud") { }
                }

                public class Order() : AuditedEntity
                {
                    public const string IdPrefix = "ord";
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID004");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Order'");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("its own IdPrefix");
    }

    [Test]
    public async Task an_entity_that_derives_from_a_concrete_entity_gets_fstid005()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                using FullstackTemplate.Server.Domain;

                public class User : BaseEntity
                {
                    public const string IdPrefix = "usr";
                    protected User() : base(IdPrefix) { }
                }

                public class Admin : User
                {
                    public new const string IdPrefix = "adm";
                    protected Admin() { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.Single(d => d.Id == "FSTID005");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Admin' derives from 'App.User'");
        // Admin cannot pass its own IdPrefix to the User constructor, so it also gets FSTID004. User gets nothing.
        diagnostics.Select(d => d.Id).Order().ShouldBe(["FSTID004", "FSTID005"]);
        diagnostics.Single(d => d.Id == "FSTID004").GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Admin'");
    }

    [Test]
    public async Task an_entity_that_derives_from_an_abstract_base_with_a_prefix_gets_fstid005()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    public const string IdPrefix = "aud";
                    protected AuditedEntity(string idPrefix) : base(idPrefix) { }
                }

                public class Tenant : AuditedEntity
                {
                    public new const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Id.ShouldBe("FSTID005");
        diagnostic.GetMessage(CultureInfo.InvariantCulture).ShouldContain("'App.Tenant' derives from 'App.AuditedEntity'");
    }

    [Test]
    public async Task an_abstract_entity_is_skipped()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public abstract class AuditedEntity : FullstackTemplate.Server.Domain.BaseEntity
                {
                    protected AuditedEntity(string idPrefix) : base(idPrefix) { }
                }

                public class Tenant : AuditedEntity
                {
                    public const string IdPrefix = "ten";
                    protected Tenant() : base(IdPrefix) { }
                }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Test]
    public async Task a_class_that_does_not_derive_from_base_entity_is_skipped()
    {
        // Act
        var diagnostics = await Run("""
            namespace App
            {
                public class BaseEntity { }
                public class Tenant : BaseEntity { }
            }
            """);

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> Run(params string[] sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            "Entities",
            [CSharpSyntaxTree.ParseText(BaseEntity), .. sources.Select((source, i) => CSharpSyntaxTree.ParseText(source, path: $"Source{i}.cs"))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation
            .WithAnalyzers([new EntityIdPrefixAnalyzer()], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return diagnostics;
    }
}
