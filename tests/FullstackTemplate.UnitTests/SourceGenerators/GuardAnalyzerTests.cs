namespace FullstackTemplate.UnitTests.SourceGenerators;

using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;

/// <summary>
/// The guards of "Claims the compiler cannot check", "Collections", "Branches", "Names", "Services", and "Tests" in
/// docs/backend/code-style.md. Each row gives the source and the IDs that the analyzer reports, so a row with no ID
/// pins a form that the guard must allow.
/// </summary>
public class GuardAnalyzerTests
{
    [Test]
    // A downcast and an unboxing need a reason. A numeric conversion cannot fail with an InvalidCastException.
    [Arguments(nameof(SafetyCommentAnalyzer), """string Of(object o) => (string)o;""", "FSTSAF001")]
    [Arguments(nameof(SafetyCommentAnalyzer), """int Of(object o) => (int)o;""", "FSTSAF001")]
    [Arguments(nameof(SafetyCommentAnalyzer), """int Of(double d) => (int)d;""", "")]
    [Arguments(nameof(SafetyCommentAnalyzer), """
        // SAFETY: Each caller passes a string.
        string Of(object o) => (string)o;
        """, "")]
    [Arguments(nameof(SafetyCommentAnalyzer), """
        string Of(object o)
        {
            var s = (string)o; // SAFETY: Each caller passes a string.
            return s;
        }
        """, "")]
    // A comment with no reason after the marker is not a reason
    [Arguments(nameof(SafetyCommentAnalyzer), """
        // SAFETY:
        string Of(object o) => (string)o;
        """, "FSTSAF001")]
    // null! and default! set a value that the constructor or EF sets. A ! on a read needs a reason.
    [Arguments(nameof(SafetyCommentAnalyzer), """
        public string Name { get; set; } = null!;
        public string Other { get; set; } = default!;
        """, "")]
    [Arguments(nameof(SafetyCommentAnalyzer), """int Of(string? s) => s!.Length;""", "FSTSAF002")]
    // A comment above an if with no braces covers the statement under it. One in another statement does not.
    [Arguments(nameof(SafetyCommentAnalyzer), """
        int Of(string? s, bool known)
        {
            // SAFETY: known is true only when s is set.
            if (known)
                return s!.Length;
            return s!.Length;
        }
        """, "FSTSAF002")]
    [Arguments(nameof(SafetyCommentAnalyzer), """
        void Of()
        {
        #pragma warning disable CS0219
            var unused = 1;
        #pragma warning restore CS0219
        }
        """, "FSTSAF003")]
    [Arguments(nameof(SafetyCommentAnalyzer), """
        void Of()
        {
        // SAFETY: The test reads the IL of this method, and the IL keeps the local.
        // The second line of the comment.
        #pragma warning disable CS0219
            var unused = 1;
        #pragma warning restore CS0219
        }
        """, "")]
    [Arguments(nameof(SafetyCommentAnalyzer), """
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060")]
        void Of(int unused) { }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060", Justification = "The signature of a delegate.")]
        void Other(int unused) { }
        """, "FSTSAF004")]
    public async Task the_safety_rules_ask_for_a_reason(string analyzer, string members, string expected)
    {
        // Act
        var ids = await Ids(analyzer, $$"""
            public class C
            {
            {{members}}
            }
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""using O = object; public class C { }""", "FSTTYP001")]
    [Arguments("""using L = System.Collections.Generic.List<int>; public class C { }""", "")]
    [Arguments("""public class C { string Of(int x) => (string)(object)x; }""", "FSTTYP002")]
    [Arguments("""public class C { string? Of(System.Uri x) => x as object as string; }""", "FSTTYP002")]
    // A numeric cast of a numeric cast, and the cast of a type parameter through object, are not a lost type
    [Arguments("""public class C { int Of(long x) => (int)(uint)x; }""", "")]
    [Arguments("""public class C { T Of<T>(int x) => (T)(object)x; }""", "")]
    // A cast pair that only widens claims nothing
    [Arguments("""public class C { object Of(object o) => (object)(string)o; }""", "")]
    [Arguments("""public class C { object? Of(object o) => o as string as object; }""", "")]
    [Arguments("""public class C { string Of(string s) { object o = s; return (string)o; } }""", "FSTTYP003")]
    [Arguments("""public class C { string Of(object x) { object o = x; return (string)o; } }""", "")]
    [Arguments("""public class C { bool Of(string s) { object o = s; return o is null || o is not null; } }""", "")]
    [Arguments("""public class C { bool Of(string s) { object o = s; return o is { } p && o is object q && o is object; } }""", "")]
    [Arguments("""public class C { bool Of(string s) { object o = s; return o is string { Length: > 0 }; } }""", "FSTTYP003")]
    public async Task the_type_rules_refuse_a_lost_type(string source, string expected)
    {
        // Act
        var ids = await Ids(nameof(TypeAssertionAnalyzer), source);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("App", """public class C { public void Of(object value) { } }""", "FSTTYP004")]
    [Arguments("App", """public class C { public Task<object?> Of() => Task.FromResult<object?>(null); }""", "FSTTYP004")]
    [Arguments("App", """public record R(dynamic Value);""", "FSTTYP004")]
    // An override or an interface implementation takes the signature of its base
    [Arguments("App", """public class C : IComparable { public override bool Equals(object? obj) => false; public override int GetHashCode() => 0; public int CompareTo(object? obj) => 0; }""", "")]
    [Arguments("App", """public class C { public object[] Of(Type type) => []; }""", "")]
    // A bag of untyped values is refused only in a Domain namespace
    [Arguments("Shop.Domain.Orders", """public class Order { public Dictionary<string, object?> Extra { get; } = []; public void Read(JsonElement json) { } }""", "FSTTYP005,FSTTYP005")]
    [Arguments("Shop.Domain.Orders", """public class Order { public List<IReadOnlyDictionary<string, object>> Rows { get; } = []; }""", "FSTTYP005")]
    [Arguments("Shop.Domain.Orders", """public class Order { public Dictionary<string, int> Counts { get; } = []; }""", "")]
    [Arguments("Shop.Http", """public class Body { public JsonElement Json { get; set; } }""", "")]
    public async Task the_signature_rules_refuse_a_hidden_type(string ns, string source, string expected)
    {
        // Act
        var ids = await Ids(nameof(SignatureTypeAnalyzer), ns: ns, source: $$"""
            using System;
            using System.Collections.Generic;
            using System.Text.Json;
            using System.Threading.Tasks;
            {{source}}
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""int Of(int[] xs) => xs.ToList().Where(x => x > 1).Count();""", "FSTLNQ001")]
    [Arguments("""List<int> Of(int[] xs) => xs.Where(x => x > 1).ToList();""", "")]
    // ToArray of a span is not a LINQ copy, and LINQ cannot read a span
    [Arguments("""int Of(int[] xs) => xs.AsSpan().ToArray().Count();""", "")]
    [Arguments("""List<int> Of(int[] xs) => xs.Aggregate(new List<int>(), (acc, x) => acc.Append(x).ToList());""", "FSTLNQ002")]
    [Arguments("""string Of(string[] xs) => xs.Aggregate("", (acc, x) => acc + x);""", "FSTLNQ002")]
    [Arguments("""int Of(int[] xs) => xs.Aggregate(0, (acc, x) => acc + x);""", "")]
    // ImmutableList adds in O(log n). ImmutableArray copies.
    [Arguments("""System.Collections.Immutable.ImmutableList<int> Of(int[] xs) => xs.Aggregate(System.Collections.Immutable.ImmutableList<int>.Empty, (acc, x) => acc.Add(x));""", "")]
    [Arguments("""System.Collections.Immutable.ImmutableArray<int> Of(int[] xs) => xs.Aggregate(System.Collections.Immutable.ImmutableArray<int>.Empty, (acc, x) => acc.Add(x));""", "FSTLNQ002")]
    [Arguments("""string Of(string[] xs) { var all = ""; foreach (var x in xs) all += x; return all; }""", "FSTLNQ003")]
    // A local of the loop body starts again on each pass
    [Arguments("""int Of(string[] xs) { var n = 0; foreach (var x in xs) { var one = x; one += "!"; n += one.Length; } return n; }""", "")]
    public async Task the_collection_rules_refuse_a_needless_copy(string members, string expected)
    {
        // Act
        var ids = await Ids(nameof(LinqCopyAnalyzer), $$"""
            using System;
            using System.Collections.Generic;
            using System.Linq;
            public class C { {{members}} }
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""
        void Of(Action act)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is InvalidOperationException)
                    return;
                throw;
            }
        }
        """, "FSTBRN001")]
    [Arguments("""
        void Of(Action act)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is not InvalidOperationException) throw;
            }
        }
        """, "FSTBRN001")]
    // A catch that handles each exception and maps one type (AppDbContext.SaveChangesAsync) has no filter form
    [Arguments("""
        void Of(Action act, Action<string> log)
        {
            try { act(); }
            catch (Exception e)
            {
                var kind = e is InvalidOperationException ? "invalid" : "other";
                log(kind);
                throw;
            }
        }
        """, "")]
    [Arguments("""
        void Of(Action act, Action<Exception> log)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is InvalidOperationException)
                    log(e);
                log(e);
            }
        }
        """, "")]
    // An if that logs one type, then a throw; of each exception, handles nothing, so a filter would change it
    [Arguments("""
        void Of(Action act, Action<Exception> log)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is InvalidOperationException)
                    log(e);
                throw;
            }
        }
        """, "")]
    [Arguments("""
        void Of(Action act, Action<Exception> log)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is InvalidOperationException) { log(e); }
                else { log(e); throw; }
            }
        }
        """, "FSTBRN001")]
    [Arguments("""
        void Of(Action act, Action<Exception> log)
        {
            try { act(); }
            catch (Exception e)
            {
                if (e is InvalidOperationException x && x.Message.Length > 0)
                    return;
                throw;
            }
        }
        """, "FSTBRN001")]
    [Arguments("""string Of(int x) => x == 1 ? "a" : x == 2 ? "b" : (x == 3 ? "c" : "d");""", "FSTBRN002")]
    [Arguments("""string Of(int x, int y) => x == 1 ? "a" : y == 2 ? "b" : "c";""", "")]
    [Arguments("""string Of(int x) => x == 1 ? "a" : "b";""", "")]
    public async Task the_branch_rules_ask_for_the_form_of_the_language(string members, string expected)
    {
        // Act
        var ids = await Ids(nameof(BranchAnalyzer), $$"""
            using System;
            public class C { {{members}} }
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""public class OrderHelper { }""", "FSTNAM001")]
    [Arguments("""public class C { public int RowData { get; } int TotalInfo() => 0; }""", "FSTNAM001,FSTNAM001")]
    [Arguments("""public class TenantManager { } public class Metadata { }""", "")]
    // An implementation takes its name from the interface, so only the interface is reported
    [Arguments("""public interface IHas { string UserData { get; } } public class C : IHas { public string UserData => ""; }""", "FSTNAM001")]
    [Arguments("""public interface IHas { string UserData { get; } } public class C : IHas { string IHas.UserData => ""; }""", "FSTNAM001")]
    public async Task the_name_rule_refuses_a_vague_suffix(string source, string expected)
    {
        // Act
        var ids = await Ids(nameof(NameSuffixAnalyzer), source);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""services.AddScoped<Clock>();""", "FSTDI001")]
    [Arguments("""services.AddScoped<IClock, Clock>();""", "FSTDI001")]
    [Arguments("""services.AddSingleton(typeof(IClock), typeof(Clock));""", "FSTDI001")]
    // A factory or an instance builds the value in code
    [Arguments("""services.AddSingleton(new Clock());""", "")]
    [Arguments("""services.AddSingleton<IClock>(_ => new Clock());""", "")]
    [Arguments("""services.AddSingleton(typeof(IClock), _ => new Clock());""", "")]
    public async Task the_service_rule_refuses_a_new_of_a_type_that_the_container_builds(string registration, string expected)
    {
        // Act
        var ids = await Ids(nameof(ServiceConstructionAnalyzer), $$"""
            using Microsoft.Extensions.DependencyInjection;
            public interface IClock { }
            public class Clock : IClock { }
            public static class Registration
            {
                public static void Add(IServiceCollection services) => {{registration}}
            }
            public class User
            {
                public IClock Make() => new Clock();
            }
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    [Test]
    [Arguments("""Substitute.For<Clock>()""", "FSTMCK001")]
    [Arguments("""Substitute.ForPartsOf<Clock>()""", "FSTMCK001")]
    [Arguments("""Substitute.For<IClock>()""", "")]
    [Arguments("""Substitute.For<AbstractClock>()""", "")]
    public async Task the_mock_rule_refuses_a_substitute_of_a_concrete_class(string substitute, string expected)
    {
        // Act
        var ids = await Ids(nameof(MockAnalyzer), $$"""
            using NSubstitute;
            public interface IClock { }
            public class Clock : IClock { public virtual int Now => 0; }
            public abstract class AbstractClock : IClock { }
            public class Test
            {
                public object Make() => {{substitute}};
            }
            """);

        // Assert
        ids.ShouldBe(expected);
    }

    private static async Task<string> Ids(string analyzer, string source, string ns = "App")
    {
        DiagnosticAnalyzer instance = analyzer switch
        {
            nameof(SafetyCommentAnalyzer) => new SafetyCommentAnalyzer(),
            nameof(TypeAssertionAnalyzer) => new TypeAssertionAnalyzer(),
            nameof(SignatureTypeAnalyzer) => new SignatureTypeAnalyzer(),
            nameof(LinqCopyAnalyzer) => new LinqCopyAnalyzer(),
            nameof(BranchAnalyzer) => new BranchAnalyzer(),
            nameof(NameSuffixAnalyzer) => new NameSuffixAnalyzer(),
            nameof(ServiceConstructionAnalyzer) => new ServiceConstructionAnalyzer(),
            nameof(MockAnalyzer) => new MockAnalyzer(),
            _ => throw new ArgumentOutOfRangeException(nameof(analyzer), analyzer, "Not a guard analyzer."),
        };

        var compilation = CSharpCompilation.Create(
            "Guards",
            [CSharpSyntaxTree.ParseText($"""
                namespace {ns};

                {source}
                """, path: "Source.cs")],
            PlatformReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();

        var exceptions = new List<Exception>();
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: (exception, _, _) => exceptions.Add(exception),
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false);
        var diagnostics = await compilation
            .WithAnalyzers([instance], options)
            .GetAnalyzerDiagnosticsAsync(TestContext.Current!.Execution.CancellationToken);

        exceptions.ShouldBeEmpty();
        return string.Join(",", diagnostics.Select(d => d.Id).Order(StringComparer.Ordinal));
    }
}
