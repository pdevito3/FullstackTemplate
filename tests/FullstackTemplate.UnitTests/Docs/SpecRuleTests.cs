namespace FullstackTemplate.UnitTests.Docs;

using Shouldly;

/// <summary>
/// Each rule of a spec in <c>docs/specs/</c> has a test that names its ID, and each ID that a test names is a rule of a
/// spec (see <c>docs/specs/TEMPLATE.md</c>). To list the faults, run
/// <c>dotnet test --project tests/FullstackTemplate.UnitTests -- --treenode-filter "/*/*/SpecRuleTests/*"</c>.
/// </summary>
public class SpecRuleTests
{
    private static readonly string[] FrontendTestFolders = ["tests", "e2e"];

    [Test]
    public void a_rule_with_no_test_and_an_id_that_no_spec_defines_are_listed()
    {
        // Arrange
        const string spec = """
            | ID | Rule | Enforced by | Backstop |
            |---|---|---|---|
            | R-1 | A work order has a title. | `WorkOrder.Create` | |
            | R-2 | A closed work order does not open again. | `WorkOrder.Reopen` | |
            | R-3 (later) | A work order has at most 20 parts. | | |
            | R-4 (later) | A part has a stock count. | | |

            | Rule | Status | Message |
            |---|---|---|
            | R-1 | 422 | Give the work order a title. |
            """;
        const string dotnetTest = """
            public class WorkOrderTests
            {
                [Test, Property("Rule", "R-1")]
                [TUnit.Core.Property("Rule","R-4")]
                public async Task a_work_order_with_no_title_is_refused() { }
            }
            """;
        const string frontendTest = """
            test('a closed work order shows no reopen button', { tag: ['@R-9'] }, async () => {})
            const admin = 'admin@ACME-1.com'
            """;
        var rules = SpecRules.Read("docs/specs/work-orders.md", spec).Rules;
        var references = SpecRules.NamedIn("tests/WorkOrderTests.cs", dotnetTest)
            .Concat(SpecRules.NamedIn("frontend/tests/work-orders.spec.ts", frontendTest))
            .ToList();

        // Act
        var faults = SpecRules.FaultsOf(rules, references);

        // Assert
        faults.ShouldBe([
            "R-2 of docs/specs/work-orders.md has no test. Name it in the test that proves it, or mark it (later) in the spec if no slice built it yet.",
            "R-4 of docs/specs/work-orders.md has a test, so a slice built it. Remove (later) from its row.",
            "frontend/tests/work-orders.spec.ts names the rule R-9, but no spec in docs/specs defines it. Fix the ID, or add the rule to the spec.",
        ]);
    }

    [Test]
    public void an_id_in_two_specs_is_listed()
    {
        // Arrange
        var rules = SpecRules.Read("docs/specs/a.md", "| ID | Rule |\n|---|---|\n| R-1 | One. |").Rules
            .Concat(SpecRules.Read("docs/specs/b.md", "| ID | Rule |\n|---|---|\n| R-1 (later) | Two. |").Rules)
            .ToList();
        var references = new List<SpecRules.Reference> { new("R-1", "tests/ATests.cs") };

        // Act
        var faults = SpecRules.FaultsOf(rules, references);

        // Assert
        faults.ShouldBe(["R-1 is defined more than once, in docs/specs/a.md and docs/specs/b.md. Give each rule its own ID."]);
    }

    [Test]
    public void a_rule_row_that_gives_no_id_is_a_fault_a_row_needs_no_leading_pipe_and_a_code_block_holds_no_rules()
    {
        // Arrange
        const string spec = """
            | **ID** | Rule |
            |:--|---|
            | `R-1` | A work order has a title. |
            | R-2(later) | A closed work order does not open again. |
            | R-3 | A part has a stock count. |

            ID | Rule
            ---|---
            R-4 | A site has a time zone.
            | R-5 | A site has a name. |

            ~~~~markdown
            ```
            | ID | Rule |
            |---|---|
            | R-6 | An example. |
            ~~~~

            | ID | Rule |
            |---|---|
            R-7 | A part has a number. |
            """;

        // Act
        var read = SpecRules.Read("docs/specs/work-orders.md", spec);

        // Assert
        read.Rules.Select(r => r.Id).ShouldBe(["R-3", "R-4", "R-5", "R-7"]);
        read.Faults.ShouldBe([
            "Line 3 of docs/specs/work-orders.md has '`R-1`' in the ID column. Write the ID as R-1, or as R-1 (later).",
            "Line 4 of docs/specs/work-orders.md has 'R-2(later)' in the ID column. Write the ID as R-1, or as R-1 (later).",
        ]);
    }

    [Test]
    public void only_a_test_method_that_runs_names_a_rule()
    {
        // Arrange
        const string source = """
            public class WorkOrderTests
            {
                [Test, Property("Rule", "R-1")]
                public async Task a_work_order_with_no_title_is_refused() { }

                [Test]
                [TUnit.Core.PropertyAttribute("Rule", "R-2")]
                public async Task a_long_title_is_refused() { }

                [Test, Skip("Not built yet"), Property("Rule", "R-3")]
                public async Task a_closed_work_order_does_not_open_again() { }

                [Property("Rule", "R-4")]
                private static void Arrange() { }

                // [Property("Rule", "R-5")]
                [Test]
                public async Task a_part_has_a_number() { }

                public const string Example = "[Property(\"Rule\", \"R-6\")]";
            }

            [Skip("Not built yet")]
            public class SiteTests
            {
                [Test, Property("Rule", "R-7")]
                public async Task a_site_has_a_time_zone() { }
            }
            """;

        // Act
        var named = SpecRules.NamedIn("tests/WorkOrderTests.cs", source);

        // Assert
        named.Select(r => r.Id).ShouldBe(["R-1", "R-2"]);
    }

    [Test]
    public void only_the_title_tag_or_annotation_of_a_frontend_test_that_runs_names_a_rule()
    {
        // Arrange
        const string source = """
            test('a closed work order shows no reopen button @R-1', async () => {})
            test('a work order can close', { tag: ['@R-2', '@smoke'] }, async () => {})
            test('a part has a number', { annotation: { type: 'rule', description: '@R-3' } }, async () => {})
            it("formats a due date @R-4", () => {})
            test(`a title with // in it @R-5`, async () => {})
            // covers @R-6
            /* @R-7 */
            test.skip('a site has a time zone @R-8', async () => {})
            test.fixme('a site has a name', { tag: '@R-9' }, async () => {})
            const note = 'see @R-10'
            const admin = 'admin@ACME-1.com'
            test.describe.skip('a group that does not run', () => { test('in a skipped group @R-11', async () => {}) })
            describe.skip('a group that does not run', () => { it('in a skipped group @R-12', () => {}) })
            it('skipped by its options', { skip: true, tag: '@R-13' }, () => {})
            const call = "test('in a string @R-14')"
            const quote = /'/
            // test('commented out after a quote @R-15', async () => {})
            """;

        // Act
        var named = SpecRules.NamedIn("frontend/tests/work-orders.spec.ts", source);

        // Assert
        named.Select(r => r.Id).ShouldBe(["R-1", "R-2", "R-3", "R-4", "R-5"]);
    }

    [Test]
    public void a_rules_section_that_the_check_cannot_read_is_a_fault()
    {
        // Arrange
        const string spec = """
            ## 3. Rules

            | Rule ID | Rule |
            |---|---|
            | P-7 | A work order has a title. |

            - P-8: A closed work order does not open again.

            ## 4. Permissions

            | Permission | Words in a message |
            |---|---|
            | `manage_work_orders` | manage work orders |
            """;

        // Act
        var read = SpecRules.Read("docs/specs/work-orders.md", spec);

        // Assert
        read.Rules.ShouldBeEmpty();
        read.Faults.ShouldBe([
            "Line 3 of docs/specs/work-orders.md starts a table in the Rules section whose first column is 'Rule ID'. Name the first column ID.",
            "Line 7 of docs/specs/work-orders.md has the rule P-8 in a list. Write each rule as a row of the table whose first column is ID.",
            "The Rules section of docs/specs/work-orders.md has no table whose first column is ID, so the check reads no rules from it. Write each rule as a row of that table, or write None.",
        ]);
    }

    [Test]
    public void a_heading_with_the_word_rules_starts_a_rules_section_up_to_the_end_of_the_file()
    {
        // Arrange
        const string spec = """
            ## 3. Business rules

            - R-2: A closed work order does not open again.
            """;

        // Act
        var read = SpecRules.Read("docs/specs/work-orders.md", spec);

        // Assert
        read.Faults.ShouldBe([
            "Line 3 of docs/specs/work-orders.md has the rule R-2 in a list. Write each rule as a row of the table whose first column is ID.",
            "The Rules section of docs/specs/work-orders.md has no table whose first column is ID, so the check reads no rules from it. Write each rule as a row of that table, or write None.",
        ]);
    }

    [Test]
    public void a_rules_section_with_a_rule_table_or_none_is_read()
    {
        // Arrange
        const string spec = """
            # Spec: work orders

            ## Rules

            A .NET test names a rule:

            - A test: `[Property("Rule", "R-1")]`.

            | ID | Rule |
            |---|---|
            | R-1 | A work order has a title. |

            ### Parts

            | ID | Rule |
            |---|---|
            | R-2 | A part has a number. |

            ## Permissions

            | Permission | Words in a message |
            |---|---|
            | `manage_work_orders` | manage work orders |
            """;

        // Act
        var read = SpecRules.Read("docs/specs/work-orders.md", spec);
        var none = SpecRules.Read("docs/specs/sites.md", "## 3. Rules\n\nNone.\n\nA slice adds the rules.\n\n## 4. Permissions\n");

        // Assert
        read.Rules.Select(r => r.Id).ShouldBe(["R-1", "R-2"]);
        read.Faults.ShouldBeEmpty();
        none.Faults.ShouldBeEmpty();
    }

    [Test]
    public void the_template_defines_its_example_rules()
    {
        // Arrange
        var markdown = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "specs", "TEMPLATE.md"));

        // Act
        var read = SpecRules.Read("docs/specs/TEMPLATE.md", markdown);

        // Assert
        read.Rules.Select(r => (r.Id, r.Later)).ShouldBe([("R-1", false), ("R-2", false), ("R-3", true)]);
        read.Faults.ShouldBeEmpty();
    }

    [Test]
    public void each_spec_rule_has_a_test_and_each_named_rule_is_in_a_spec()
    {
        // Arrange
        var root = FindRepositoryRoot();
        var specs = Directory.EnumerateFiles(Path.Combine(root, "docs", "specs"), "*.md", SearchOption.AllDirectories)
            .Where(file => Relative(root, file) != "docs/specs/TEMPLATE.md")
            .Select(file => SpecRules.Read(Relative(root, file), File.ReadAllText(file)))
            .ToList();
        var references = TestFiles(root)
            .SelectMany(file => SpecRules.NamedIn(file, File.ReadAllText(Path.Combine(root, file))))
            .ToList();

        // Act
        var faults = specs.SelectMany(s => s.Faults)
            .Concat(SpecRules.FaultsOf([.. specs.SelectMany(s => s.Rules)], references));

        // Assert
        faults.ShouldBeEmpty();
    }

    private static IEnumerable<string> TestFiles(string root)
    {
        var dotnet = Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories);
        var frontendSuites = FrontendTestFolders
            .Select(folder => Path.Combine(root, "frontend", folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.ts*", SearchOption.AllDirectories));
        var frontendUnits = Directory.EnumerateFiles(Path.Combine(root, "frontend", "src"), "*.test.ts*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".ts", StringComparison.Ordinal) || file.EndsWith(".tsx", StringComparison.Ordinal));

        return dotnet.Concat(frontendSuites).Concat(frontendUnits)
            .Select(file => Relative(root, file))
            .Where(file => !file.Split('/').Intersect(["bin", "obj"]).Any());
    }

    private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FullstackTemplate.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Could not find FullstackTemplate.slnx above the test output directory.");
    }
}
