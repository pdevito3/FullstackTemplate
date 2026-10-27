namespace FullstackTemplate.UnitTests.SourceGenerators;

using System.Reflection;
using System.Runtime.Loader;
using FullstackTemplate.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;

public class ValueObjectGeneratorTests
{
    private const string Codes = """
        namespace App
        {
            using System.Collections.Generic;
            using FullstackTemplate.Server.Domain;

            public partial class Codes : ValueObject
            {
                public Codes(IReadOnlyList<string>? names, int[] numbers, string label)
                {
                    Names = names;
                    Numbers = numbers;
                    Label = label;
                }

                public IReadOnlyList<string>? Names { get; }
                public int[] Numbers { get; }
                public string Label { get; }
            }
        }
        """;

    [Test]
    public void value_objects_with_the_same_items_in_new_lists_are_equal()
    {
        // Arrange
        var codes = Compile(Codes).GetType("App.Codes")!;
        var first = New(codes, new List<string> { "a", "b" }, [1, 2], "x");
        var second = New(codes, new List<string> { "a", "b" }, [1, 2], "x");

        // Act & Assert
        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Test]
    public void value_objects_with_other_items_or_another_order_are_not_equal()
    {
        // Arrange
        var codes = Compile(Codes).GetType("App.Codes")!;
        var value = New(codes, new List<string> { "a", "b" }, [1, 2], "x");

        // Act & Assert
        value.Equals(New(codes, new List<string> { "b", "a" }, [1, 2], "x")).ShouldBeFalse();
        value.Equals(New(codes, new List<string> { "a", "b" }, [1, 3], "x")).ShouldBeFalse();
        value.Equals(New(codes, new List<string> { "a" }, [1, 2], "x")).ShouldBeFalse();
        value.Equals(New(codes, null, [1, 2], "x")).ShouldBeFalse();
        value.Equals(New(codes, new List<string> { "a", "b" }, [1, 2], "y")).ShouldBeFalse();
    }

    [Test]
    public void value_objects_with_no_list_are_equal()
    {
        // Arrange
        var codes = Compile(Codes).GetType("App.Codes")!;
        var first = New(codes, null, [], "x");
        var second = New(codes, null, [], "x");

        // Act & Assert
        first.Equals(second).ShouldBeTrue();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Test]
    public void a_value_object_with_a_member_of_no_single_item_type_compiles()
    {
        // Act
        var assembly = Compile("""
            namespace App
            {
                using System.Collections;
                using System.Collections.Generic;
                using FullstackTemplate.Server.Domain;

                public sealed class Pairs : IEnumerable<int>, IEnumerable<string>
                {
                    IEnumerator<int> IEnumerable<int>.GetEnumerator() => new List<int>().GetEnumerator();
                    IEnumerator<string> IEnumerable<string>.GetEnumerator() => new List<string>().GetEnumerator();
                    IEnumerator IEnumerable.GetEnumerator() => new List<int>().GetEnumerator();
                }

                public partial class Grid : ValueObject
                {
                    public int[,] Cells { get; } = new int[1, 1];
                    public Pairs Pairs { get; } = new();
                }
            }
            """);

        // Assert
        var grid = assembly.GetType("App.Grid")!;
        Activator.CreateInstance(grid)!.Equals(Activator.CreateInstance(grid)).ShouldBeFalse();
    }

    private static object New(Type codes, List<string>? names, List<int> numbers, string label)
        => Activator.CreateInstance(codes, names, numbers.ToArray(), label)!;

    private static Assembly Compile(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            $"ValueObjects{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver.Create(new ValueObjectGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);
        generated.GetDiagnostics().Where(d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();

        using var image = new MemoryStream();
        generated.Emit(image).Success.ShouldBeTrue();
        image.Position = 0;
        return new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(image);
    }
}
