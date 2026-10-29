namespace FullstackTemplate.UnitTests.Architecture;

using Shouldly;

/// <summary>
/// Each <c>[Test]</c> of the .NET suites marks its parts with <c>// Arrange</c>, <c>// Act</c>, and <c>// Assert</c>
/// (see <c>docs/backend.md</c>, Tests).
/// </summary>
public class TestMarkerTests
{
    [Test]
    public void markers_in_order_pass()
    {
        // Arrange
        const string source = """
            public class Tests
            {
                [Test]
                public void full()
                {
                    // Arrange
                    var x = 1;

                    // Act
                    var y = x + 1;

                    // Assert
                    y.ShouldBe(2);
                }

                [Test]
                public async Task joined()
                {
                    // Act & Assert
                    await Should.ThrowAsync<Exception>(() => Run());
                }

                [Test]
                public async Task markers_in_a_lambda_or_a_local_function_do_not_count()
                {
                    // Arrange
                    var run = async () =>
                    {
                        // Act
                        await Task.Yield();
                    };

                    // Act
                    await run();

                    // Assert
                    Check(1).ShouldBe(2);

                    static int Check(int value)
                    {
                        // Act
                        return value + 1;
                    }
                }

                [Test]
                public void arrange_and_act_with_a_note()
                {
                    // Arrange and act - the clinic closes at noon
                    var y = Close();

                    // Assert: the visit moves
                    y.ShouldBeTrue();
                }
            }
            """;

        // Act
        var violations = TestMarkers.Of(source);

        // Assert
        violations.ShouldBeEmpty();
    }

    [Test]
    public void the_shapes_that_carepoint_shipped_fail()
    {
        // Arrange
        const string source = """
            public class VisitTests
            {
                [Test]
                public void no_markers()
                {
                    var visit = new Visit();
                    visit.Cancel();
                    visit.IsCancelled.ShouldBeTrue();
                }

                [Test]
                public void code_before_the_first_marker()
                {
                    var clock = new FakeClock();
                    // Arrange
                    var visit = new Visit(clock);

                    // Act
                    visit.Cancel();

                    // Assert
                    visit.IsCancelled.ShouldBeTrue();
                }

                [Test]
                public void out_of_order()
                {
                    // Arrange
                    var visit = new Visit();

                    // Assert
                    visit.IsCancelled.ShouldBeFalse();

                    // Act
                    visit.Cancel();
                }

                [Test]
                public void no_act()
                {
                    // Arrange
                    var visit = new Visit();

                    // Assert
                    visit.IsCancelled.ShouldBeFalse();
                }

                [Test]
                public void marker_before_the_brace()
                    // Arrange
                {
                    var visit = new Visit();

                    // Act
                    visit.Cancel();

                    // Assert
                    visit.IsCancelled.ShouldBeTrue();
                }

                [Test]
                public void expression_body() => new Visit().IsCancelled.ShouldBeFalse();
            }
            """;

        // Act
        var violations = TestMarkers.Of(source).ToDictionary(v => v.Test, v => v.Reason);

        // Assert
        violations.ShouldBe(new Dictionary<string, string>
        {
            ["VisitTests.no_markers"] = "has no markers",
            ["VisitTests.code_before_the_first_marker"] = "has code before its first marker",
            ["VisitTests.out_of_order"] = "has the markers Arrange, Assert, Act, not in the order Arrange, Act, Assert",
            ["VisitTests.no_act"] = "has no // Act",
            ["VisitTests.marker_before_the_brace"] = "has code before its first marker",
            ["VisitTests.expression_body"] = "has an expression body, so it has no markers",
        }, ignoreOrder: true);
    }

    [Test]
    public void a_comment_that_starts_with_a_marker_word_is_not_a_marker()
    {
        // Arrange
        const string source = """
            public class Tests
            {
                [Test]
                public void prose()
                {
                    // Asserts that the visit moves
                    // Arrange, Act
                    // Act & 2
                    var y = Close();
                    // act on the result
                    y.ShouldBeTrue();
                }
            }
            """;

        // Act
        var violations = TestMarkers.Of(source).Select(v => v.Reason);

        // Assert
        violations.ShouldBe(["has no markers"]);
    }

    [Test]
    public void each_test_marks_its_parts()
    {
        // Arrange
        var root = FindRepositoryRoot();

        // Act
        var violations = Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => !file.Split('/').Intersect(["bin", "obj"]).Any())
            .SelectMany(file => TestMarkers.Of(File.ReadAllText(Path.Combine(root, file)))
                .Select(v => $"{file} {v.Test} {v.Reason}"))
            .Order()
            .ToList();

        // Assert
        violations.ShouldBeEmpty("Mark the parts of these tests with // Arrange, // Act, and // Assert (see docs/backend.md, Tests).");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
