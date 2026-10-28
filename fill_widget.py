#!/usr/bin/env python3
"""Fill every backend-feature template for a scratch Widget entity, or remove the scratch code again.

Usage:
  fill_widget.py fill  <repo> [--templates <templates.md>]
  fill_widget.py clean <repo>

fill writes each **File:** template of templates.md with the placeholders replaced, then makes the edits that
SKILL.md asks for: the DbSet, the permission, the admin grant migration, the entity graph blueprint, and the test
lists. It fails when a placeholder stays in a filled file. clean restores the edited files and deletes the new ones.
"""
import re
import subprocess
import sys
from pathlib import Path

SERVER = "FullstackTemplate.Server"

BASE = {
    "[EntityName]": "Widget",
    "[entity-route]": "widget",
    "[entity_name]": "widget",
    "[entity name]": "widget",
    "[prefix]": "wdg",
    "[Criteria]": "Tenant",
    "[criteria]": "tenant",
    "[criteria-route]": "tenant",
    "[criteria_name]": "tenant",
}

# The action templates and the domain event template come in two variants with the same file path.
ACTIONS = [
    {"[Action]": "Archive", "[action_name]": "archive", "[action-route]": "archive", "[ActionPastTense]": "Archived", "[Action words]": "Archived",
     "[Description of the action]": "Archives the widget"},
    {"[Action]": "Rename", "[action_name]": "rename", "[action-route]": "rename", "[ActionPastTense]": "Renamed", "[Action words]": "Renamed",
     "[Description of the action]": "Renames the widget"},
]

ATTRIBUTES = {"Mapper", "NotSensitive", "Sensitive", "RequiredField", "McpServerToolType", "GraphGraft"}

EDITED = [
    f"{SERVER}/Databases/AppDbContext.cs",
    f"{SERVER}/Domain/Users/Permission.cs",
    f"{SERVER}/Databases/Migrations/AppDbContextModelSnapshot.cs",
    f"{SERVER}/openapi",
    "tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/Blueprints.cs",
    "tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/Inputs.cs",
    "tests/FullstackTemplate.UnitTests/Databases/StringColumnLengthTests.cs",
    "tests/FullstackTemplate.UnitTests/Domain/Users/PermissionTests.cs",
]
CREATED_DIRS = [f"{SERVER}/Domain/Widgets"]
CREATED_FILES = [f"{SERVER}/Databases/EntityConfigurations/WidgetConfiguration.cs"]
MIGRATION_GLOB = "*_AddWidgets*.cs"


def read(path: Path) -> str:
    return path.read_bytes().decode("utf-8-sig").replace("\r\n", "\n")


def write(path: Path, text: str, crlf: bool = True) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if crlf:
        text = text.replace("\n", "\r\n")
    path.write_bytes(text.encode("utf-8"))


def edit(path: Path, anchor: str, insert: str, after: bool = True) -> None:
    raw = path.read_bytes().decode("utf-8")
    crlf = "\r\n" in raw
    text = raw.replace("\r\n", "\n")
    if anchor not in text:
        sys.exit(f"anchor not found in {path}: {anchor!r}")
    text = text.replace(anchor, anchor + insert if after else insert + anchor, 1)
    write(path, text, crlf)


def substitute(text: str, mapping: dict) -> str:
    for key in sorted(mapping, key=len, reverse=True):
        text = text.replace(key, mapping[key])
    return text


def leftover_placeholders(text: str) -> list:
    found = re.findall(r"\[([A-Za-z_\- ]+)\]", text)
    return sorted({f for f in found if f not in ATTRIBUTES})


def file_templates(templates: str) -> list:
    pattern = re.compile(r"\*\*File:\*\* `([^`]+)`\s*\n+```csharp\n(.*?)\n```", re.S)
    return [(m.group(1), m.group(2) + "\n") for m in pattern.finditer(templates)]


def fill_templates(repo: Path, templates: str) -> list:
    seen = {}
    written = []
    problems = []
    for path, body in file_templates(templates):
        variant = seen.get(path, 0)
        seen[path] = variant + 1
        mapping = {**BASE, **ACTIONS[min(variant, len(ACTIONS) - 1)]}
        target = substitute(path, mapping)
        text = substitute(body, mapping)
        left = leftover_placeholders(text)
        if left:
            problems.append(f"{target}: placeholder not replaced: {', '.join(left)}")
        dest = repo / SERVER / target
        write(dest, text)
        written.append(target)
    return written, problems


DOMAIN_METHODS = """
    [NotSensitive] public DateTimeOffset? ArchivedAt { get; private set; }

    public Widget Archive()
    {
        ArchivedAt = DateTimeOffset.UtcNow;
        QueueDomainEvent(new WidgetArchived(Id, ArchivedAt.Value));
        return this;
    }

    public Widget Rename(string parameter1, string parameter2)
    {
        Name = parameter1;
        ValidateWidget(this);
        QueueDomainEvent(new WidgetRenamed(Id, parameter2, DateTimeOffset.UtcNow));
        return this;
    }

"""

RENAME_DTO = """namespace FullstackTemplate.Server.Domain.Widgets.Dtos;

using Resources;

public sealed record RenameWidgetDto
{
    [RequiredField]
    public string Parameter1 { get; init; } = default!;
    [RequiredField]
    public string Parameter2 { get; init; } = default!;
}
"""


def fill(repo: Path, templates_path: Path) -> None:
    templates = read(templates_path)
    written, problems = fill_templates(repo, templates)

    # The second domain event variant has no file label of its own. It goes in the file of the first variant.
    reason_event = re.search(r"If the action takes a reason.*?\n```csharp\n(.*?)\n```", templates, re.S).group(1)
    mapping = {**BASE, **ACTIONS[1]}
    reason_text = substitute(reason_event + "\n", mapping)
    if leftover_placeholders(reason_text):
        problems.append(f"reason event: placeholder not replaced: {leftover_placeholders(reason_text)}")
    write(repo / SERVER / "Domain/Widgets/DomainEvents/WidgetRenamed.cs", reason_text)
    written.append("Domain/Widgets/DomainEvents/WidgetRenamed.cs")

    # The domain method template is a sketch with a status value object, so the actions get small real methods.
    edit(repo / SERVER / "Domain/Widgets/Widget.cs", "    // Business methods\n", DOMAIN_METHODS, after=False)
    write(repo / SERVER / "Domain/Widgets/Dtos/RenameWidgetDto.cs", RENAME_DTO)
    edit(repo / SERVER / "Domain/Widgets/Dtos/WidgetDto.cs", "    public DateTimeOffset? LastModifiedOn { get; init; }\n",
         "    public DateTimeOffset? ArchivedAt { get; init; }\n")

    dbset = re.search(r"\*\*Add DbSet to `AppDbContext.cs`:\*\*\s*\n+```csharp\n(.*?)\n```", templates, re.S).group(1)
    ctx = repo / SERVER / "Databases/AppDbContext.cs"
    edit(ctx, "using Domain.Users;\n", "using Domain.Widgets;\n")
    edit(ctx, "    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();\n", "    " + substitute(dbset, BASE) + "\n")

    perm = repo / SERVER / "Domain/Users/Permission.cs"
    edit(perm, "    public static Permission ReadAgents() => new(PermissionEnum.ReadAgents.Name);\n",
         "    public static Permission ManageWidgets() => new(PermissionEnum.ManageWidgets.Name);\n")
    edit(perm, '        public const string ReadAgents = "read_agents";\n',
         '        public const string ManageWidgets = "manage_widgets";\n')
    edit(perm, "        public static readonly PermissionEnum ReadAgents = new(Names.ReadAgents, 7);\n",
         "        public static readonly PermissionEnum ManageWidgets = new(Names.ManageWidgets, 8);\n")

    blueprints = repo / "tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/Blueprints.cs"
    edit(blueprints, "using FullstackTemplate.Server.Domain.Users.Models;\n",
         "using FullstackTemplate.Server.Domain.Widgets;\nusing FullstackTemplate.Server.Domain.Widgets.Models;\n")
    edit(blueprints, "        .Derive(i => i.Permissions, (_, _) => Array.Empty<string>());\n", """
    public static readonly ChildInputBlueprint<TestSeed, Tenant, WidgetInput, Widget> Widget = Blueprint
        .ChildFromInput<TestSeed, Tenant, WidgetInput, Widget>(
            () => new(),
            (tenant, input, _) => Server.Domain.Widgets.Widget.Create(new WidgetForCreation
            {
                Id = input.Id,
                TenantId = tenant.Id,
                Name = input.Name!,
                Description = input.Description,
            }))
        .Derive(i => i.Name, (_, _) => Faker.Commerce.ProductName());
""")
    edit(repo / "tests/FullstackTemplate.SharedTestHelpers/EntityGraphs/Inputs.cs", "public sealed class AgentInput\n", """public sealed class WidgetInput
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

""", after=False)

    lengths = repo / "tests/FullstackTemplate.UnitTests/Databases/StringColumnLengthTests.cs"
    edit(lengths, '        ["agents.client_id"] = new("clientId", Text, v => TestGraph.Start(out _).AddTenant(out _).AddAgent(out _, a => a.ClientId = v)),\n',
         '        ["widgets.name"] = new("name", Text, v => TestGraph.Start(out _).AddTenant(out _).AddWidget(out _, w => w.Name = v)),\n'
         '        ["widgets.description"] = new("description", Text, v => TestGraph.Start(out _).AddTenant(out _).AddWidget(out _, w => w.Description = v)),\n')

    tests = repo / "tests/FullstackTemplate.UnitTests/Domain/Users/PermissionTests.cs"
    raw = tests.read_bytes().decode("utf-8")
    tests.write_bytes(raw.replace('"read_users", "read_agents"]', '"read_users", "read_agents", "manage_widgets"]').encode("utf-8"))

    for p in problems:
        print(f"placeholder problem: {p}")
    subprocess.run(["dotnet", "ef", "migrations", "add", "AddWidgets", "--project", SERVER, "--startup-project", SERVER,
                    "--output-dir", "Databases/Migrations"], cwd=repo, check=False).returncode == 0 or sys.exit(
        "dotnet ef migrations add failed. Run dotnet build on the Server to see the errors.")
    migration = next(p for p in (repo / SERVER / "Databases/Migrations").glob("*_AddWidgets.cs"))
    edit(migration, "            migrationBuilder.CreateTable(\n",
         '            migrationBuilder.GrantPermissionToRole("Admin", "manage_widgets");\n\n', after=False)
    edit(migration, "            migrationBuilder.DropTable(\n",
         '            migrationBuilder.DeletePermission("manage_widgets");\n\n', after=False)

    report(written, problems)


def report(written: list, problems: list) -> None:
    print("filled templates:")
    for path in written:
        print(f"  {path}")
    if problems:
        print("placeholder problems:")
        for p in problems:
            print(f"  {p}")
        sys.exit(2)


def clean(repo: Path) -> None:
    subprocess.run(["git", "checkout", "--", *EDITED], cwd=repo, check=True)
    for d in CREATED_DIRS:
        subprocess.run(["rm", "-rf", str(repo / d)], check=True)
    for f in CREATED_FILES:
        (repo / f).unlink(missing_ok=True)
    for p in (repo / SERVER / "Databases/Migrations").glob(MIGRATION_GLOB):
        p.unlink()
    status = subprocess.run(["git", "status", "--porcelain", "--", SERVER, "tests"], cwd=repo, check=True,
                            capture_output=True, text=True).stdout
    if status.strip():
        sys.exit(f"scratch code is still in the tree:\n{status}")
    print("clean: no scratch code in FullstackTemplate.Server or tests")


def main() -> None:
    if len(sys.argv) < 3 or sys.argv[1] not in {"fill", "clean"}:
        sys.exit(__doc__)
    repo = Path(sys.argv[2]).resolve()
    if sys.argv[1] == "clean":
        clean(repo)
        return
    templates = repo / ".agents/skills/backend-feature/templates.md"
    if "--templates" in sys.argv:
        templates = Path(sys.argv[sys.argv.index("--templates") + 1]).resolve()
    fill(repo, templates)


if __name__ == "__main__":
    main()
