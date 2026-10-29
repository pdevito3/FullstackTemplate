// Writes a new tenant-owned entity: the entity, its EF configuration and migration, the DTOs and the mapper, the add,
// get, list, update, and delete features, the entrypoints, the permission, and the unit, integration, and functional
// tests. See .agents/skills/backend-feature/SKILL.md.
//
//   dotnet run scripts/new-feature.cs -- SupplyItem --fields "Name:text, Notes:text?, Quantity:int, IsActive:bool"
//   dotnet run scripts/new-feature.cs -- SupplyItem --fields "..." --entrypoints http,grpc,mcp --plural SupplyItems --prefix sup
//   dotnet run scripts/new-feature.cs -- Delivery --fields "Title:text, SupplyItem:ref, DueOn:zoned-date"
//
// A second run with the same input changes nothing. A run that would change a file that exists stops before it writes.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

try
{
    var root = Path.GetFullPath(Path.Combine((string)AppContext.GetData("EntryPointFileDirectoryPath")!, ".."));
    var repo = Repo.Load(root);
    var messagesOnly = args.Contains("--messages");
    var spec = Spec.Parse([.. args.Where(a => a != "--messages")], repo.App, repo.ProtoPackage);
    if (messagesOnly)
    {
        foreach (var line in Texts.All(spec))
            Console.WriteLine(line);
        return 0;
    }

    spec = spec.CheckedIn(repo);
    var plan = Plan.For(spec, repo);
    repo.RequireModelThatMatches(spec);
    var result = repo.Apply(plan, spec);
    var migration = repo.AddMigration(spec);
    if (result.Written.Count > 0 && migration.Count == 0)
        repo.RequireNoPendingModelChangesAfterWrite(spec, result);

    Console.WriteLine(result.Written.Count + migration.Count == 0
        ? $"The {spec.Names.Words} feature is already in the app. The script changed nothing."
        : $"Wrote {result.Written.Count} files and {migration.Count} migration files.");
    foreach (var file in result.Written.Concat(migration))
        Console.WriteLine($"  {file}");
    Console.WriteLine($"""

        Next:
        1. Write the domain rules in {spec.Names.EntityFile}: the limits, the messages, and the [Sensitive] marks.
        2. Update the API contract of the SPA. See .agents/skills/api-contract/SKILL.md.
        3. dotnet build {repo.App}.slnx
        4. dotnet test --solution {repo.App}.slnx
        """);
    if (spec.Parent is { } parent)
        Console.WriteLine($"""
            5. Decide what the delete of {parent.Entity} does with its {spec.Names.WordsPlural}: refuse, delete them too, or clear
               the reference. Load and save in RootLock.Tenant.RunExclusiveAsync, so that an add cannot link to it in the
               middle. See docs/backend/domain-events.md and docs/backend/concurrency.md.
            """);
    return 0;
}
catch (FeatureException exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"The script stopped: {exception.Message}");
    return 1;
}

internal sealed class FeatureException(string message) : Exception(message);

internal enum Kind { Text, Int, Bool, Date, Instant, ZonedDate, TimeZone, ParentId }

[Flags]
internal enum Entrypoints { None = 0, Http = 1, Grpc = 2, Mcp = 4 }

internal sealed record Field(string Name, Kind Kind, bool Optional, bool Sensitive)
{
    public string Camel => Naming.Identifier(Naming.Camel(Name));
    public string Json => Naming.Camel(Name);
    public string Snake => Naming.Snake(Name);
    public string Words => Naming.Words(Kind == Kind.ParentId ? Name[..^"Id".Length] : Name);

    public string Label => char.ToUpperInvariant(Words[0]) + Words[1..];

    public bool NeedsMark => Kind is not (Kind.Int or Kind.Bool);

    public bool CreateOnly => Kind is Kind.ParentId or Kind.TimeZone;

    public string Type => Kind switch
    {
        Kind.Text => Optional ? "string?" : "string",
        Kind.Int => "int",
        Kind.Bool => "bool",
        Kind.Date or Kind.ZonedDate => "DateOnly",
        Kind.Instant => "DateTimeOffset",
        Kind.TimeZone or Kind.ParentId => "string",
    };
}

internal sealed record Parent(string Entity, string Namespace, string Plural, string BlueprintNamespace)
{
    public string Var => Naming.Identifier(Naming.Camel(Entity));
    public string Snake => Naming.Snake(Entity);
}

internal sealed record Names(string App, string Entity, string Plural, string Prefix, string ProtoPackage)
{
    public string Server => $"{App}.Server";
    public string Ns => $"{Server}.Domain.{Plural}";
    public string Var => Naming.Identifier(Naming.Camel(Entity));
    public string Snake => Naming.Snake(Entity);
    public string SnakePlural => Naming.Snake(Plural);
    public string KebabPlural => SnakePlural.Replace('_', '-');
    public string Words => Naming.Words(Entity);
    public string WordsPlural => Naming.Words(Plural);
    public string A => Naming.Article(Words);
    public string Permission => $"Manage{Plural}";
    public string PermissionName => $"manage_{SnakePlural}";
    public string PermissionWords => $"manage {WordsPlural}";
    public string Folder => $"{Server}/Domain/{Plural}";
    public string EntityFile => $"{Folder}/{Entity}.cs";
    public string GrpcPackage => $"{ProtoPackage}.{SnakePlural}.v1";
    public string GrpcService => $"{Plural}Service";
    public string Migration => $"Add{Plural}";
}

internal sealed record Spec(Names Names, IReadOnlyList<Field> Fields, Entrypoints Entrypoints, Parent? Parent = null)
{
    public Field Title => Fields[0];
    public bool Has(Entrypoints entrypoint) => Entrypoints.HasFlag(entrypoint);
    public Field? Ref => Fields.FirstOrDefault(f => f.Kind == Kind.ParentId);
    public bool HasZone => Fields.Any(f => f.Kind == Kind.TimeZone);

    public IEnumerable<Field> Updatable => Fields.Where(f => !f.CreateOnly);

    public IEnumerable<Field> CreationFields => Fields.Where(f => f.Kind != Kind.ParentId);

    public string ParentGraphStep => Parent is { } p ? $".Add{p.Entity}(out _)" : "";

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
        { "Id", "TenantId", "Version", "LastModifiedOn", "IsDeleted", "DomainEvents", "IdPrefix", "Create", "Update", "Delete" };

    private const string Usage = """
        Usage: dotnet run scripts/new-feature.cs -- <Entity> --fields "<Name:kind>, ..." [options]

        <Entity>       The PascalCase name of the entity, for example SupplyItem.
        --fields       The properties. A kind is text, text?, int, bool, date, zoned-date, instant, or ref. The first
                       property must be text: the list searches and sorts by it. Add :public to text, date,
                       zoned-date, or instant when the value cannot hold personal data. Each other value of these
                       kinds is [Sensitive] in the audit log.
                       A zoned-date is a day in the time zone of the entity, so the script adds the property TimeZone.
                       Carrier:ref is the property CarrierId: the ID of a Carrier of the same tenant. An entity has
                       at most one ref, and the add sets it.
                       Example: "Name:text:public, Carrier:ref, Quantity:int, IsActive:bool, DueOn:zoned-date"
        --entrypoints  A list of http, grpc, and mcp, or none. The default is http.
        --plural       The plural of the entity. The default adds s, es, or ies.
        --prefix       The prefix of the ID: 1 to 17 lower-case letters. The default is the first 3 letters.
        --messages     Print each text of the feature that a person reads, and change no file.
        """;

    public static Spec Parse(string[] args, string app, string protoPackage)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
            throw new FeatureException(Usage);

        var entity = args[0];
        string? fields = null, plural = null, prefix = null, entrypoints = null;
        for (var i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length)
                throw new FeatureException($"The option {args[i]} has no value.\n\n{Usage}");
            var value = args[i + 1];
            switch (args[i])
            {
                case "--fields": fields = value; break;
                case "--plural": plural = value; break;
                case "--prefix": prefix = value; break;
                case "--entrypoints": entrypoints = value; break;
                default: throw new FeatureException($"The option {args[i]} is not known.\n\n{Usage}");
            }
        }

        RequirePascalCase(entity, "The entity name");
        plural ??= Naming.Plural(entity);
        RequirePascalCase(plural, "The plural");
        if (plural == entity)
            throw new FeatureException("The plural must not be the same as the entity name. Give --plural.");
        prefix ??= new string(entity.ToLowerInvariant().Take(3).ToArray());
        if (!Regex.IsMatch(prefix, "^[a-z]{1,17}$"))
            throw new FeatureException($"The ID prefix '{prefix}' must have 1 to 17 lower-case letters. Give --prefix.");

        var parsed = ParseFields(fields ?? throw new FeatureException($"Give the properties with --fields.\n\n{Usage}"));
        if (parsed.Any(f => f.Name == entity))
            throw new FeatureException($"A property must not have the name of the entity, {entity}.");
        if (parsed.Any(f => f.Kind == Kind.ParentId && f.Name == $"{entity}Id"))
            throw new FeatureException($"An entity cannot refer to itself. Remove {entity}:ref.");
        var points = ParseEntrypoints(entrypoints ?? "http");
        if (points.HasFlag(Entrypoints.Grpc) && parsed.FirstOrDefault(f => f.Kind is Kind.Date or Kind.ZonedDate) is { } date)
            throw new FeatureException(
                $"The property {date.Name} is a date, and protobuf has no date type. Use instant, or leave out grpc.");

        return new Spec(new Names(app, entity, plural, prefix, protoPackage), parsed, points);
    }

    public Spec CheckedIn(Repo repo)
    {
        var n = Names;
        if (repo.HidesServerNamespace(n.Plural))
            throw new FeatureException(
                $"The namespace Domain.{n.Plural} would hide the namespace {n.Server}.{n.Plural} in the domain code. Give another --plural.");
        if (repo.TypeOwner(n.Entity, n.EntityFile) is { } typeOwner)
            throw new FeatureException($"The type {n.Entity} is in use in {typeOwner}. Give another entity name.");
        if (repo.IdPrefixOwner(n.Prefix, n.EntityFile) is { } owner)
            throw new FeatureException($"The ID prefix '{n.Prefix}' is in use in {owner}. Give another with --prefix.");
        if (Ref is not { } reference)
            return this;

        var parent = repo.FindParent(reference.Name[..^"Id".Length]);
        if (parent.Entity == "User")
            throw new FeatureException("A ref to User is not possible: the delete of a user must handle each record that "
                + "links to it (docs/backend/domain-events.md). Add the UserId property by hand after the run.");
        if (parent.Namespace.Contains(".Examples.", StringComparison.Ordinal))
            throw new FeatureException($"{parent.Entity} is an example, and scripts/remove-examples.cs deletes it. "
                + "Give a ref to an entity of the app.");
        if (LocalNames.Contains(parent.Var))
            throw new FeatureException($"The code that the script writes has a variable '{parent.Var}', so a ref to "
                + $"{parent.Entity} would not build. Add the {reference.Name} property by hand after the run.");
        return this with { Parent = parent };
    }

    // The variables of the code that the script writes, in the scopes where the variable of the parent goes
    private static readonly HashSet<string> LocalNames = new(StringComparer.Ordinal)
    {
        "act", "add", "added", "admin", "ct", "delete", "dto", "graph", "loaded", "member", "parentId", "pause",
        "refusal", "request", "rival", "seed", "services", "tenant", "tenantExists", "tenantId", "testingServiceScope",
        "underTest", "update", "user",
    };

    private static List<Field> ParseFields(string text)
    {
        var fields = new List<Field>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(':', StringSplitOptions.TrimEntries);
            if (pieces.Length is < 2 or > 3)
                throw new FeatureException($"The property '{part}' must be Name:kind or Name:kind:public.");
            var (given, kindText) = (pieces[0], pieces[1]);
            RequirePascalCase(given, $"The property name '{given}'");
            var optional = kindText.EndsWith('?');
            var kind = kindText.TrimEnd('?') switch
            {
                "text" => Kind.Text,
                "int" => Kind.Int,
                "bool" => Kind.Bool,
                "date" => Kind.Date,
                "zoned-date" => Kind.ZonedDate,
                "instant" => Kind.Instant,
                "ref" => Kind.ParentId,
                _ => throw new FeatureException(
                    $"The kind '{kindText}' of {given} is not known. Use text, text?, int, bool, date, zoned-date, instant, or ref."),
            };
            var name = kind == Kind.ParentId ? $"{given}Id" : given;
            if (Reserved.Contains(name))
                throw new FeatureException($"The property name {name} is reserved. Each entity has a member with this name.");
            if (fields.Any(f => f.Name == name))
                throw new FeatureException($"The property {name} is in the list two times.");

            if (optional && kind == Kind.ParentId)
                throw new FeatureException($"A ref cannot be optional. Remove the '?' from {given}.");
            if (optional && kind != Kind.Text)
                throw new FeatureException($"Only text can be optional. Make {name} text?, or remove the '?'.");

            var isPublic = pieces.Length == 3
                ? pieces[2] == "public"
                    ? true
                    : throw new FeatureException($"The mark '{pieces[2]}' of {name} is not known. Use public.")
                : false;
            if (isPublic && kind is Kind.Int or Kind.Bool or Kind.ParentId)
                throw new FeatureException($"A value of the kind {kindText} needs no mark. Remove :public from {given}.");

            fields.Add(new Field(name, kind, optional,
                Sensitive: kind is Kind.Text or Kind.Date or Kind.ZonedDate or Kind.Instant && !isPublic));
        }

        if (fields.Count == 0 || fields[0] is not { Kind: Kind.Text, Optional: false })
            throw new FeatureException("The first property must be required text, such as Name:text. The list searches and sorts by it.");
        if (fields.Count(f => f.Kind == Kind.ParentId) > 1)
            throw new FeatureException("An entity can have one ref. Make the other references by hand after the run.");

        if (fields.FindIndex(f => f.Kind == Kind.ZonedDate) is var first and >= 0)
        {
            if (fields.Any(f => f.Name == "TimeZone"))
                throw new FeatureException("A zoned-date adds the property TimeZone. Give the other property another name.");
            fields.Insert(first, new Field("TimeZone", Kind.TimeZone, Optional: false, Sensitive: false));
        }
        return fields;
    }

    private static Entrypoints ParseEntrypoints(string text)
    {
        if (text == "none")
            return Entrypoints.None;
        var result = Entrypoints.None;
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            result |= part switch
            {
                "http" => Entrypoints.Http,
                "grpc" => Entrypoints.Grpc,
                "mcp" => Entrypoints.Mcp,
                _ => throw new FeatureException($"The entrypoint '{part}' is not known. Use http, grpc, mcp, or none."),
            };
        }
        return result;
    }

    private static void RequirePascalCase(string name, string what)
    {
        if (!Regex.IsMatch(name, "^(?:[A-Z][a-z0-9]+)+$"))
            throw new FeatureException(
                $"{what} '{name}' must be PascalCase with no two capitals in a row, for example SupplyItem or SkuCode.");
    }
}

internal static class Naming
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc",
        "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
        "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    public static string Camel(string pascal) => char.ToLowerInvariant(pascal[0]) + pascal[1..];

    public static string Identifier(string name) => Keywords.Contains(name) ? "@" + name : name;

    public static string Snake(string pascal) => Regex.Replace(pascal, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();

    public static string Words(string pascal) => Snake(pascal).Replace('_', ' ');

    private static readonly string[] SoundStartsWithConsonant = ["uni", "use", "usa", "usu", "uti", "ure", "uri", "eu", "one", "once"];
    private static readonly string[] SoundStartsWithVowel = ["hour", "honest", "honor", "honour", "heir"];

    public static string Article(string words)
    {
        bool StartsWithAny(string[] prefixes) => prefixes.Any(p => words.StartsWith(p, StringComparison.Ordinal));
        if (StartsWithAny(SoundStartsWithVowel))
            return "an";
        if (StartsWithAny(SoundStartsWithConsonant))
            return "a";
        return "aeiou".Contains(words[0]) ? "an" : "a";
    }

    public static string Plural(string pascal)
    {
        if (Regex.IsMatch(pascal, "[^aeiou]y$"))
            return pascal[..^1] + "ies";
        return Regex.IsMatch(pascal, "(?:s|x|z|ch|sh)$") ? pascal + "es" : pascal + "s";
    }
}

/// <summary>A file that the feature owns. The script writes it only when it is missing.</summary>
internal sealed record FileWrite(string Path, string Text);

/// <summary>
/// Lines for a file that many features share, such as AppDbContext. The script puts them above the line
/// <c>// new-feature:{Slot}</c>, with its indent. When the file holds <see cref="Probe"/>, the lines are in already.
/// </summary>
/// <param name="WhenFileExists">The file is an example that scripts/remove-examples.cs can delete.</param>
internal sealed record Insertion(string Path, string Slot, string Probe, IReadOnlyList<string> Lines, bool WhenFileExists = false);

internal sealed record Plan(IReadOnlyList<FileWrite> Files, IReadOnlyList<Insertion> Insertions)
{
    public static Plan For(Spec spec, Repo repo)
    {
        var n = spec.Names;
        var files = new List<FileWrite>();
        void Add(string path, string text) => files.Add(new FileWrite(path, text.ReplaceLineEndings("\n").TrimEnd('\n') + "\n"));

        var server = n.Folder;
        Add(n.EntityFile, Templates.Entity(spec));
        Add($"{server}/DomainEvents/{n.Entity}Created.cs", Templates.Event(spec, "Created"));
        Add($"{server}/DomainEvents/{n.Entity}Updated.cs", Templates.Event(spec, "Updated"));
        Add($"{server}/Models/{n.Entity}ForCreation.cs", Templates.ForCreation(spec));
        Add($"{server}/Models/{n.Entity}ForUpdate.cs", Templates.ForUpdate(spec));
        Add($"{server}/Dtos/{n.Entity}Dto.cs", Templates.Dto(spec));
        Add($"{server}/Dtos/{n.Entity}ForCreationDto.cs", Templates.ForCreationDto(spec));
        Add($"{server}/Dtos/{n.Entity}ForUpdateDto.cs", Templates.ForUpdateDto(spec));
        Add($"{server}/Dtos/{n.Entity}ParametersDto.cs", Templates.ParametersDto(spec));
        Add($"{server}/Mappings/{n.Entity}Mapper.cs", Templates.Mapper(spec));
        Add($"{n.Server}/Databases/EntityConfigurations/{n.Entity}Configuration.cs", Templates.Configuration(spec));

        var features = $"{server}/Features";
        Add($"{features}/Add{n.Entity}/Add{n.Entity}.cs", Templates.AddFeature(spec));
        Add($"{features}/Get{n.Entity}/Get{n.Entity}.cs", Templates.GetFeature(spec));
        Add($"{features}/Get{n.Entity}List/Get{n.Entity}List.cs", Templates.ListFeature(spec));
        Add($"{features}/Update{n.Entity}/Update{n.Entity}.cs", Templates.UpdateFeature(spec));
        Add($"{features}/Delete{n.Entity}/Delete{n.Entity}.cs", Templates.DeleteFeature(spec));

        if (spec.Has(Entrypoints.Http))
        {
            Add($"{features}/Add{n.Entity}/Add{n.Entity}.Http.cs", Templates.AddHttp(spec));
            Add($"{features}/Get{n.Entity}/Get{n.Entity}.Http.cs", Templates.GetHttp(spec));
            Add($"{features}/Get{n.Entity}List/Get{n.Entity}List.Http.cs", Templates.ListHttp(spec));
            Add($"{features}/Update{n.Entity}/Update{n.Entity}.Http.cs", Templates.UpdateHttp(spec));
            Add($"{features}/Delete{n.Entity}/Delete{n.Entity}.Http.cs", Templates.DeleteHttp(spec));
        }

        if (spec.Has(Entrypoints.Mcp))
        {
            Add($"{features}/Add{n.Entity}/Add{n.Entity}.Mcp.cs", Templates.AddMcp(spec));
            Add($"{features}/Get{n.Entity}/Get{n.Entity}.Mcp.cs", Templates.GetMcp(spec));
            Add($"{features}/Get{n.Entity}List/Get{n.Entity}List.Mcp.cs", Templates.ListMcp(spec));
            Add($"{features}/Update{n.Entity}/Update{n.Entity}.Mcp.cs", Templates.UpdateMcp(spec));
        }

        if (spec.Has(Entrypoints.Grpc))
        {
            var grpc = $"{server}/Grpc/v1";
            Add($"{grpc}/{n.SnakePlural}.proto", Templates.Proto(spec));
            Add($"{grpc}/{n.Plural}GrpcService.cs", Templates.GrpcService(spec));
            Add($"{grpc}/{n.Entity}GrpcMappings.cs", Templates.GrpcMappings(spec));
            Add($"{features}/Add{n.Entity}/Add{n.Entity}.Grpc.cs", Templates.AddGrpc(spec));
            Add($"{features}/Get{n.Entity}/Get{n.Entity}.Grpc.cs", Templates.GetGrpc(spec));
            Add($"{features}/Get{n.Entity}List/Get{n.Entity}List.Grpc.cs", Templates.ListGrpc(spec));
            Add($"{features}/Update{n.Entity}/Update{n.Entity}.Grpc.cs", Templates.UpdateGrpc(spec));
            Add($"{features}/Delete{n.Entity}/Delete{n.Entity}.Grpc.cs", Templates.DeleteGrpc(spec));
        }

        var helpers = $"tests/{n.App}.SharedTestHelpers";
        Add($"{helpers}/EntityGraphs/{n.Entity}Input.cs", Templates.GraphInput(spec));
        Add($"{helpers}/EntityGraphs/{n.Entity}Blueprints.cs", Templates.Blueprint(spec));
        Add($"{helpers}/Fakes/{n.Plural}/{n.Entity}Rules.cs", Templates.Rules(spec));
        Add($"{helpers}/Fakes/{n.Plural}/Fake{n.Entity}ForCreationDto.cs", Templates.FakeForCreationDto(spec));

        Add($"tests/{n.App}.UnitTests/Domain/{n.Plural}/{n.Entity}Tests.cs", Templates.UnitTests(spec));
        var integration = $"tests/{n.App}.IntegrationTests/FeatureTests/{n.Plural}";
        Add($"{integration}/{n.Entity}CommandTests.cs", Templates.CommandTests(spec));
        Add($"{integration}/{n.Entity}ListQueryTests.cs", Templates.ListQueryTests(spec));
        Add($"{integration}/{n.Entity}TenantDeleteRaceTests.cs", Templates.TenantDeleteRaceTests(spec));
        var functional = $"tests/{n.App}.FunctionalTests/FunctionalTests/{n.Plural}";
        if (spec.Has(Entrypoints.Http))
            Add($"{functional}/{n.Entity}HttpTests.cs", Templates.HttpTests(spec));
        if (spec.Has(Entrypoints.Mcp))
            Add($"{functional}/{n.Entity}McpToolTests.cs", Templates.McpTests(spec));
        if (spec.Has(Entrypoints.Grpc))
            Add($"{functional}/{n.Entity}GrpcTests.cs", Templates.GrpcTests(spec));

        return new Plan(files, InsertionsFor(spec, repo));
    }

    private static List<Insertion> InsertionsFor(Spec spec, Repo repo)
    {
        var n = spec.Names;
        var permission = $"tests/{n.App}.UnitTests/Domain/Users/PermissionTests.cs";
        var sweep = $"tests/{n.App}.FunctionalTests/FunctionalTests/Access/EndpointAccessSweepTests.cs";
        var permissionFile = $"{n.Server}/Domain/Users/Permission.cs";
        var value = repo.NextPermissionValue(permissionFile, n.Permission);
        var step = spec.ParentGraphStep;
        var sweepParent = spec.Parent is not null ? $"parentOf{n.Entity}" : null;
        var columnsTest = $"tests/{n.App}.UnitTests/Databases/StringColumnLengthTests.cs";

        var list = new List<Insertion>
        {
            new($"{n.Server}/Databases/AppDbContext.cs", "db-usings", $"using Domain.{n.Plural};", [$"using Domain.{n.Plural};"]),
            new($"{n.Server}/Databases/AppDbContext.cs", "db-sets", $"DbSet<{n.Entity}>",
                [$"public DbSet<{n.Entity}> {n.Plural} => Set<{n.Entity}>();"]),
            new(permissionFile, "permission-factories", $"Permission {n.Permission}()",
                [$"public static Permission {n.Permission}() => new(PermissionEnum.{n.Permission}.Name);"]),
            new(permissionFile, "permission-names", $"const string {n.Permission} =",
                [$"public const string {n.Permission} = \"{n.PermissionName}\";"]),
            new(permissionFile, "permission-values", $"PermissionEnum {n.Permission} =",
                [$"public static readonly PermissionEnum {n.Permission} = new(Names.{n.Permission}, {value}, \"{n.PermissionWords}\");"]),
            new(permission, "permission-names", $"\"{n.PermissionName}\",", [$"\"{n.PermissionName}\","]),
            new($"tests/{n.App}.IntegrationTests/FeatureTests/Tenancy/TenantDeleteOwnedRowsTests.cs", "tenant-owned-rows",
                $".Add{n.Entity}(out _)", [$".Under(tenant){step}.Add{n.Entity}(out _)"]),
            new(sweep, "sweep-graph", $".Add{n.Entity}(out var {n.Var})",
                [.. spec.Parent is { } owner ? [$".Add{owner.Entity}(out var {sweepParent})"] : Array.Empty<string>(),
                    $".Add{n.Entity}(out var {n.Var})", ".Under(tenant)"]),
            new(sweep, "sweep-inserts", $"InsertAsync({n.Var})",
                [.. sweepParent is null ? [] : new[] { $"await InsertAsync({sweepParent});" }, $"await InsertAsync({n.Var});"]),
            new(sweep, "sweep-ids", $"[\"{n.KebabPlural}\"] =", [$"[\"{n.KebabPlural}\"] = {n.Var}.Id,"]),
        };

        var columns = spec.Fields.Where(f => f.Kind == Kind.Text).Select(f =>
            $"[\"{n.SnakePlural}.{f.Snake}\"] = new(\"{f.Json}\", Text, v => TestGraph.Start(out _){step}.Add{n.Entity}(out _, x => x.{f.Name} = v)),").ToList();
        list.Add(new(columnsTest, "caller-columns", $"[\"{n.SnakePlural}.{spec.Title.Snake}\"]", columns));
        if (spec.Fields.FirstOrDefault(f => f.Kind == Kind.TimeZone) is { } zone)
            list.Add(new(columnsTest, "fixed-option-columns", $"[\"{n.SnakePlural}.{zone.Snake}\"]",
                [$"[\"{n.SnakePlural}.{zone.Snake}\"] = TimeZoneInfo.GetSystemTimeZones().Where(z => z.HasIanaId).Select(z => z.Id).ToList(),"]));

        var open = new List<string>();
        if (spec.Has(Entrypoints.Http))
        {
            open.Add($"[\"Get{n.Entity}\"] = new(\"Each member reads the {n.WordsPlural} of the own tenant. The tenant filter hides each other tenant.\"),");
            open.Add($"[\"Get{n.Entity}List\"] = new(\"Each member lists the {n.WordsPlural} of the own tenant. The tenant filter hides each other tenant.\"),");
        }
        if (spec.Has(Entrypoints.Grpc))
        {
            open.Add($"[\"/{n.GrpcPackage}.{n.GrpcService}/Get{n.Entity}\"] = new(\"The gRPC method of Get{n.Entity}.\"),");
            open.Add($"[\"/{n.GrpcPackage}.{n.GrpcService}/List{n.Plural}\"] = new(\"The gRPC method of Get{n.Entity}List.\"),");
        }
        if (spec.Has(Entrypoints.Mcp))
        {
            list.Add(new($"tests/{n.App}.FunctionalTests/Examples/Tenants/TenantExampleMcpToolTests.cs", "mcp-tools",
                $"\"get_{n.Snake}\",", [$"\"add_{n.Snake}\", \"get_{n.Snake}\", \"list_{n.SnakePlural}\", \"update_{n.Snake}\","],
                WhenFileExists: true));
            open.Add($"[\"get_{n.Snake}\"] = new(\"ToolAccessKind.Read. Each member reads the {n.WordsPlural} of the own tenant, and the tenant filter hides the others.\"),");
            open.Add($"[\"list_{n.SnakePlural}\"] = new(\"ToolAccessKind.Read. Each member lists the {n.WordsPlural} of the own tenant, and the tenant filter hides the others.\"),");
        }
        list.AddRange(open.Select(line => new Insertion(sweep, "open-to-each-member", line[..line.IndexOf('=')], [line])));

        if (spec.Has(Entrypoints.Grpc))
        {
            var registration = $"{n.Server}/Resources/Entrypoints/EntrypointRegistration.cs";
            list.Add(new(registration, "grpc-usings", $"using Domain.{n.Plural}.Grpc.V1;", [$"using Domain.{n.Plural}.Grpc.V1;"]));
            list.Add(new(registration, "grpc-services", $"MapGrpcService<{n.Plural}GrpcService>",
                [$"app.MapGrpcService<{n.Plural}GrpcService>();"]));
        }
        return list;
    }
}

internal sealed record ApplyResult(IReadOnlyList<string> Written);

internal sealed partial class Repo(string root, string app, string protoPackage)
{
    public string App => app;
    public string ProtoPackage => protoPackage;

    public static Repo Load(string root)
    {
        var app = Directory.GetFiles(root, "*.slnx") is [var solution]
            ? Path.GetFileNameWithoutExtension(solution)
            : throw new FeatureException($"The folder {root} must hold exactly one .slnx file.");
        var tenants = Path.Combine(root, $"{app}.Server", "Domain", "Tenants", "Grpc", "v1", "tenants.proto");
        var package = File.Exists(tenants) && TenantsPackage().Match(File.ReadAllText(tenants)) is { Success: true } match
            ? match.Groups[1].Value
            : app.ToLowerInvariant();
        return new Repo(root, app, package);
    }

    /// <summary>The file that declares the prefix, other than <paramref name="ownFile"/>, or null.</summary>
    public string? IdPrefixOwner(string prefix, string ownFile)
    {
        var probe = $"IdPrefix = \"{prefix}\"";
        return ServerFiles()
            .Where(file => file != ownFile)
            .FirstOrDefault(file => File.ReadAllText(Path.Combine(root, file)).Contains(probe, StringComparison.Ordinal));
    }

    private IEnumerable<string> ServerFiles()
        => Directory.EnumerateFiles(Path.Combine(root, $"{app}.Server"), "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => !file.Contains("/obj/") && !file.Contains("/bin/"));

    public Parent FindParent(string entity)
    {
        var declaration = new Regex($@"\bclass\s+{entity}\s*:[^{{]*\bBaseEntity\b[^{{]*\bITenantable\b");
        var entityText = ServerFiles()
            .Select(file => File.ReadAllText(Path.Combine(root, file)))
            .FirstOrDefault(declaration.IsMatch)
            ?? throw new FeatureException(
                $"{entity}:ref names no entity that a tenant owns in {app}.Server. Give the name of an entity that derives from BaseEntity and is ITenantable"
                + (entity.EndsWith("Id", StringComparison.Ordinal) ? $", with no Id: {entity[..^"Id".Length]}:ref." : "."));

        var contextFile = $"{app}.Server/Databases/AppDbContext.cs";
        var plural = Regex.Match(File.ReadAllText(Path.Combine(root, contextFile)), $@"\bDbSet<{entity}>\s+(\w+)") is { Success: true } set
            ? set.Groups[1].Value
            : throw new FeatureException($"{contextFile} has no DbSet of {entity}, so the add cannot load it.");

        var blueprint = new Regex($@"ChildInputBlueprint<TestSeed,\s*(\w+),\s*{entity}Input,\s*{entity}>");
        var helpers = Path.Combine(root, "tests", $"{app}.SharedTestHelpers");
        var blueprintText = Directory.EnumerateFiles(helpers, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(File.ReadAllText)
            .FirstOrDefault(blueprint.IsMatch)
            ?? throw new FeatureException(
                $"The entity graph has no blueprint of {entity}, so the tests cannot add one. Add {entity} with the script first.");
        if (blueprint.Match(blueprintText).Groups[1].Value is var owner and not "Tenant")
            throw new FeatureException($"A ref can name only an entity that is right under the tenant. {entity} is under {owner}.");

        return new Parent(entity, NamespaceOf(entityText), plural, NamespaceOf(blueprintText));
    }

    private static string NamespaceOf(string text) => FileNamespace().Match(text).Groups[1].Value;

    public bool HidesServerNamespace(string plural)
        => plural == "Domain" || Directory.Exists(Path.Combine(root, $"{app}.Server", plural));

    /// <summary>The file that declares the type, other than <paramref name="ownFile"/>, or null.</summary>
    public string? TypeOwner(string type, string ownFile)
    {
        var declaration = new Regex($@"\b(?:class|record|struct|interface|enum)\s+{type}\b");
        return ServerFiles()
            .Where(file => file != ownFile)
            .FirstOrDefault(file => declaration.IsMatch(File.ReadAllText(Path.Combine(root, file))));
    }

    /// <summary>The value of the permission when it exists, else one more than the highest value.</summary>
    public int NextPermissionValue(string permissionFile, string member)
    {
        var text = File.ReadAllText(Path.Combine(root, permissionFile));
        var values = PermissionValue().Matches(text).ToDictionary(m => m.Groups[1].Value, m => int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        if (values.Count == 0)
            throw new FeatureException($"{permissionFile} has no PermissionEnum value that the script can read.");
        return values.TryGetValue(member, out var own) ? own : values.Values.Max() + 1;
    }

    /// <summary>Checks each file before it writes one, so that a refusal changes nothing.</summary>
    public ApplyResult Apply(Plan plan, Spec spec)
    {
        var conflicts = new List<string>();
        var writes = new List<FileWrite>();
        foreach (var file in plan.Files)
        {
            var path = Path.Combine(root, file.Path);
            if (!File.Exists(path))
                writes.Add(file);
            else if (File.ReadAllText(path).ReplaceLineEndings("\n") != file.Text)
                conflicts.Add(file.Path);
        }

        var edits = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var insertion in plan.Insertions)
        {
            var path = Path.Combine(root, insertion.Path);
            if (insertion.WhenFileExists && !File.Exists(path))
                continue;
            if (!edits.TryGetValue(insertion.Path, out var text))
                text = File.Exists(path)
                    ? File.ReadAllText(path)
                    : throw new FeatureException($"The script edits {insertion.Path}, but the file does not exist.");
            if (text.Contains(insertion.Probe, StringComparison.Ordinal))
                continue;

            var marker = $"// new-feature:{insertion.Slot}";
            var markers = Regex.Matches(text, $@"^([ \t]*){Regex.Escape(marker)}[ \t]*\r?$", RegexOptions.Multiline);
            if (markers.Count != 1)
                throw new FeatureException(
                    $"{insertion.Path} must have exactly one line '{marker}'. The script puts the lines of a new feature above it.");
            var indent = markers[0].Groups[1].Value;
            var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var block = string.Concat(insertion.Lines.Select(line => indent + line + newline));
            edits[insertion.Path] = text.Insert(markers[0].Index, block);
        }

        if (conflicts.Count > 0)
            throw new FeatureException(
                "The script changed no files. These files exist and differ from what it writes:\n  "
                + string.Join("\n  ", conflicts)
                + "\nThe script writes only new files. If an older run wrote them, make the change by hand.\n"
                + UndoAdvice(spec));

        foreach (var file in writes)
        {
            var path = Path.Combine(root, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, file.Text);
        }
        foreach (var (file, text) in edits)
            File.WriteAllText(Path.Combine(root, file), text);

        return new ApplyResult([.. writes.Select(w => w.Path), .. edits.Keys.Order(StringComparer.Ordinal)]);
    }

    public void RequireModelThatMatches(Spec spec)
    {
        var n = spec.Names;
        if (File.Exists(Path.Combine(root, n.EntityFile)))
            return;

        var context = $"{Server}/Databases/AppDbContext.cs";
        var parts = MigrationFiles(spec);
        if (File.ReadAllText(Path.Combine(root, context)).Contains($"DbSet<{n.Entity}>", StringComparison.Ordinal))
            parts.Add(context);
        if (parts.Count > 0)
            throw new FeatureException($"""
                The script changed no files. {n.EntityFile} is not in the app, but these parts of the feature are:
                  {string.Join("\n  ", parts)}
                A run now would write an entity that the migration and the model snapshot do not match.
                {UndoAdvice(spec)}
                """);

        BuildServer("The script changed no files. The Server does not build, so dotnet-ef cannot check its model. "
            + "Correct the errors above, then run the script again.");
        RunEf(NoChangeFailure, "migrations", "has-pending-model-changes", "--project", Server, "--startup-project", Server, "--no-build");
    }

    public static string UndoAdvice(Spec spec) => $"""
        To give the feature other properties, undo the first run, and then run the script again. Restore each file
        that the run changed, the model snapshot too, and delete each file that it added, the migration {spec.Names.Migration} too.
        When the run is not committed, git restore and git clean do this. When it is, git revert of its commit does.
        """;

    public void RequireNoPendingModelChangesAfterWrite(Spec spec, ApplyResult result)
    {
        var written = "\n  " + string.Join("\n  ", result.Written);
        BuildServer("The script wrote the files below, and the Server does not build. Correct the errors above. To undo the run, "
            + "restore the shared files and delete the new files with git restore and git clean." + written);
        RunEf($"The script wrote the files below, and the migration {spec.Names.Migration} was in the app already. The model of "
            + "the Server has changes that no migration has, from this run or from other work. Add a migration of the changes, "
            + "or undo the run with git restore and git clean." + written,
            "migrations", "has-pending-model-changes", "--project", Server, "--startup-project", Server, "--no-build");
    }

    private string Server => $"{app}.Server";

    private const string NoChangeFailure =
        "The script changed no files. The model of the Server has changes that no migration has, or dotnet-ef could not " +
        "check it. See the output above. Add the migration of those changes, then run the script again.";

    private const string MigrationFailure =
        "The script wrote the feature files but no migration, because the Server does not build. A run again refuses a "
        + "changed file, so undo the run with git restore and git clean, and file the build error as an issue of the template.";

    private List<string> MigrationFiles(Spec spec)
        => Directory.EnumerateFiles(Path.Combine(root, Server, "Databases", "Migrations"),
                $"*_{spec.Names.Migration}*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Where(file => Regex.IsMatch(Path.GetFileName(file), $@"^\d+_{spec.Names.Migration}(?:\.Designer)?\.cs$"))
            .Order(StringComparer.Ordinal)
            .ToList();

    // dotnet-ef hides the compiler errors of its own build, so the script builds first and gives --no-build
    private void BuildServer(string failure) => Run("dotnet", ["build", Path.Combine(Server, $"{Server}.csproj")], failure);

    /// <summary>Adds the migration once. It gives the new permission to each admin that exists.</summary>
    public IReadOnlyList<string> AddMigration(Spec spec)
    {
        var n = spec.Names;
        var folder = Path.Combine(root, Server, "Databases", "Migrations");
        if (MigrationFiles(spec).Count > 0)
            return [];

        BuildServer(MigrationFailure);
        RunEf(MigrationFailure, "migrations", "add", n.Migration, "--project", Server, "--startup-project", Server,
            "--output-dir", "Databases/Migrations", "--no-build");

        var migration = Directory.GetFiles(folder, $"*_{n.Migration}.cs").Single();
        var text = File.ReadAllText(migration);
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var down = text.IndexOf("protected override void Down(MigrationBuilder migrationBuilder)", StringComparison.Ordinal);
        var upEnd = down < 0 ? -1 : text.LastIndexOf($"{newline}        }}", down, StringComparison.Ordinal);
        if (upEnd < 0)
            throw new FeatureException($"The script could not find Up and Down in {migration}. Add the permission lines by hand.");
        var downStart = text.IndexOf($"{{{newline}", down, StringComparison.Ordinal) + 1 + newline.Length;

        text = text.Insert(downStart, $"            migrationBuilder.DeletePermission(\"{n.PermissionName}\");{newline}{newline}");
        text = text.Insert(upEnd, $"{newline}{newline}            migrationBuilder.GrantPermissionToRole(\"Admin\", \"{n.PermissionName}\");");
        File.WriteAllText(migration, text);

        return Directory.GetFiles(folder, $"*_{n.Migration}*.cs")
            .Append(Directory.GetFiles(folder, "*ModelSnapshot.cs").Single())
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // Not a local tool: on Linux, a manifest with dotnet-ef and another tool fails dotnet tool restore (SDK 10.0.403)
    private void RunEf(string failure, params string[] arguments)
    {
        var efVersion = XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .Single(p => (string?)p.Attribute("Include") == "Microsoft.EntityFrameworkCore.Design")
            .Attribute("Version")!.Value;
        Run("dotnet", ["tool", "exec", $"dotnet-ef@{efVersion}", "--yes", "--", .. arguments], failure);
    }

    private void Run(string command, string[] arguments, string failure)
    {
        var line = $"{command} {string.Join(' ', arguments)}";
        Console.WriteLine($"> {line}");
        try
        {
            using var process = Process.Start(new ProcessStartInfo(command, arguments) { WorkingDirectory = root })
                ?? throw new FeatureException($"The script could not start {command}.");
            process.WaitForExit();
            if (process.ExitCode != 0)
                throw new FeatureException($"'{line}' failed. {failure}");
        }
        catch (Win32Exception exception)
        {
            throw new FeatureException($"The script could not start {command}: {exception.Message}");
        }
    }

    [GeneratedRegex(@"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex FileNamespace();

    [GeneratedRegex(@"^package\s+([a-z0-9_]+)\.tenants\.v1\s*;", RegexOptions.Multiline)]
    private static partial Regex TenantsPackage();

    [GeneratedRegex(@"PermissionEnum\s+(\w+)\s*=\s*\r?\n?\s*new\(Names\.\w+,\s*(\d+)\s*,")]
    private static partial Regex PermissionValue();
}

internal static class Texts
{
    private static string QuotedLabel(Field f) => $"\"{f.Label}\" value";

    public static string Missing(Spec s, Field f) => $"Please give the {s.Names.Words} a value for \"{f.Label}\".";

    public static string TooLong(Spec s, Field f, string limit)
        => $"The {QuotedLabel(f)} of {s.Names.A} {s.Names.Words} can have no more than {limit} characters.";

    public static string BadCharacter(Spec s, Field f) => $"The {QuotedLabel(f)} of {s.Names.A} {s.Names.Words} has a character that is not allowed.";

    public static string OutOfRange(Spec s, Field f, string minimum, string maximum)
        => $"The {QuotedLabel(f)} of {s.Names.A} {s.Names.Words} is from {minimum} to {maximum}.";

    public static string AddToOtherTenant(Spec s) => $"You can add {s.Names.A} {s.Names.Words} only to your own tenant.";

    public static string ParentMissing(Field f) => $"The {f.Words} does not exist. Choose another {f.Words}.";

    public static string ToolParameter(Spec s, Field f) => f.Kind switch
    {
        Kind.Date => $"The {QuotedLabel(f)} of the {s.Names.Words}, a date such as 2026-01-02.",
        Kind.ZonedDate => $"The {QuotedLabel(f)} of the {s.Names.Words}, a date such as 2026-01-02 in the time zone of the {s.Names.Words}.",
        Kind.TimeZone => $"The time zone of the {s.Names.Words}, an IANA ID such as America/Chicago. An update does not change it.",
        Kind.ParentId => $"The ID of the {f.Words} of the {s.Names.Words}. An ID that the caller cannot see gives the invalid_argument error. An update does not change it.",
        Kind.Instant => $"The {QuotedLabel(f)} of the {s.Names.Words}, an ISO 8601 instant such as 2026-01-02T10:00:00Z.",
        Kind.Int => $"The {QuotedLabel(f)} of the {s.Names.Words}, a whole number. A value out of range gives the invalid_argument error.",
        Kind.Bool => $"The {QuotedLabel(f)} of the {s.Names.Words}: true or false.",
        Kind.Text when f.Optional => $"The {QuotedLabel(f)} of the {s.Names.Words}. Leave it out for none.",
        _ => $"The {QuotedLabel(f)} of the {s.Names.Words}.",
    };

    public static string AddTool(Spec s)
        => $"Adds {s.Names.A} {s.Names.Words} to the caller's tenant. The caller needs the {s.Names.PermissionName} permission in the tenant.";

    public static string GetTool(Spec s) => $"Gets one {s.Names.Words} of the caller's tenant by its ID.";

    public static string ListTool(Spec s) => $"Gets one page of the {s.Names.WordsPlural} of the caller's tenant, with the page data.";

    public static string Search(Spec s) => $"A text that the {QuotedLabel(s.Title)} contains, with no regard to case.";

    public static string Filters(Spec s)
        => $"A QueryKit filter on the fields of {s.Names.A} {s.Names.Words}, for example: {s.Title.Json} @=* \"text\"";

    public static string UpdateTool(Spec s)
        => $"Updates {s.Names.A} {s.Names.Words} of the caller's tenant. The caller needs the {s.Names.PermissionName} permission in the tenant.";

    public static IEnumerable<string> All(Spec s)
    {
        yield return $"add to other tenant: {AddToOtherTenant(s)}";
        foreach (var f in s.Fields)
        {
            switch (f.Kind)
            {
                case Kind.Text:
                    if (!f.Optional)
                        yield return $"{f.Name} missing: {Missing(s, f)}";
                    yield return $"{f.Name} too long: {TooLong(s, f, $"{{{f.Name}MaxLength}}")}";
                    yield return $"{f.Name} bad character: {BadCharacter(s, f)}";
                    break;
                case Kind.Int:
                    yield return $"{f.Name} out of range: {OutOfRange(s, f, $"{{{f.Name}Minimum}}", $"{{{f.Name}Maximum}}")}";
                    break;
                case Kind.Date or Kind.ZonedDate or Kind.Instant:
                    yield return $"{f.Name} missing: {Missing(s, f)}";
                    break;
                case Kind.ParentId:
                    yield return $"{f.Name} not found: {ParentMissing(f)}";
                    break;
            }
            yield return $"{f.Name} tool parameter: {ToolParameter(s, f)}";
        }
        yield return $"add tool: {AddTool(s)}";
        yield return $"get tool: {GetTool(s)}";
        yield return $"list tool: {ListTool(s)}";
        yield return $"list search: {Search(s)}";
        yield return $"list filters: {Filters(s)}";
        yield return $"update tool: {UpdateTool(s)}";
    }
}

/// <summary>
/// The text of each file. A block of lines goes into a template at column 0 and carries its own indent.
/// </summary>
internal static class Templates
{
    private static string Block(IEnumerable<string> lines, int indent)
    {
        var pad = new string(' ', indent);
        return string.Join("\n", lines.SelectMany(line => line.Split('\n')).Select(line => line.Length == 0 ? "" : pad + line));
    }

    private static string Mark(Field f) => f.NeedsMark ? (f.Sensitive ? "[Sensitive] " : "[NotSensitive] ") : "";

    private static string EscapeForCs(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string Missing(Spec s, Field f) => EscapeForCs(Texts.Missing(s, f));

    private static string TooLong(Spec s, Field f, string owner) => EscapeForCs(Texts.TooLong(s, f, $"{{{owner}{f.Name}MaxLength}}"));

    private static string BadCharacter(Spec s, Field f) => EscapeForCs(Texts.BadCharacter(s, f));

    private static string OutOfRange(Spec s, Field f, string owner)
        => EscapeForCs(Texts.OutOfRange(s, f, $"{{{owner}{f.Name}Minimum}}", $"{{{owner}{f.Name}Maximum}}"));

    private static bool IsRequiredString(Field f) => f.Kind is Kind.TimeZone or Kind.ParentId || f is { Kind: Kind.Text, Optional: false };

    private static string ModelProperty(Field f) => IsRequiredString(f)
        ? $"public string {f.Name} {{ get; init; }} = default!;"
        : $"public {f.Type} {f.Name} {{ get; init; }}";

    private static string Assign(Field f, string source) => f.Kind switch
    {
        Kind.Instant => $"{f.Name} = {source}.{f.Name}.ToUniversalTime()",
        Kind.TimeZone => $"{f.Name} = TimeZoneId.Of({source}.{f.Name})",
        _ => $"{f.Name} = {source}.{f.Name}",
    };

    private static IEnumerable<string> CopyForUpdate(Spec s, string source)
        => s.Updatable.Select(f => $"{f.Name} = {source}.{f.Name},").Append($"Version = {source}.Version,");

    private static string Usings(params IEnumerable<string?>[] groups)
        => string.Join("\n", groups.SelectMany(g => g).OfType<string>().Distinct()
            .OrderBy(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(u => u, StringComparer.Ordinal)
            .Select(u => $"using {u};"));

    public static string Entity(Spec s)
    {
        var (n, p) = (s.Names, s.Parent);
        var constants = s.Fields.SelectMany(f => f.Kind switch
        {
            Kind.Text => [$"public const int {f.Name}MaxLength = 200;"],
            Kind.Int => new[] { $"public const int {f.Name}Minimum = 0;", $"public const int {f.Name}Maximum = 10_000;" },
            _ => [],
        });
        var properties = s.Fields.Select(f => f.Kind == Kind.TimeZone
            ? $"{Mark(f)}public TimeZoneId {f.Name} {{ get; private set; }} = default!;"
            : IsRequiredString(f)
                ? $"{Mark(f)}public string {f.Name} {{ get; private set; }} = default!;"
                : $"{Mark(f)}public {f.Type} {f.Name} {{ get; private set; }}");
        var create = s.Fields.Select(f => f.Kind == Kind.ParentId ? $"{f.Name} = {p!.Var}.Id," : Assign(f, $"{n.Var}ForCreation") + ",");
        var update = s.Updatable.Select(f => Assign(f, $"{n.Var}ForUpdate") + ";");
        var same = string.Join("\n            && ", s.Updatable.Select(f => f.Kind == Kind.Instant
            ? $"{n.Var}ForUpdate.{f.Name}.ToUniversalTime() == {f.Name}"
            : $"{n.Var}ForUpdate.{f.Name} == {f.Name}"));
        var rules = s.Fields.SelectMany(f => Rules(s, f, n.Var));
        var parameters = p is null ? $"{n.Entity}ForCreation {n.Var}ForCreation" : $"{p.Entity} {p.Var}, {n.Entity}ForCreation {n.Var}ForCreation";
        var tenant = p is null ? $"{n.Var}ForCreation.TenantId" : $"{p.Var}.TenantId";
        var usings = Usings(["DomainEvents", "Exceptions", "Models", "Tenants", s.HasZone ? "TimeZones" : null, p?.Namespace]);

        return $$"""
            namespace {{n.Ns}};

            {{usings}}

            public class {{n.Entity}} : BaseEntity, ITenantable
            {
                public const string IdPrefix = "{{n.Prefix}}";
            {{Block(constants, 4)}}

                [NotSensitive] public string TenantId { get; private set; } = default!;
            {{Block(properties, 4)}}

                public static {{n.Entity}} Create({{parameters}})
                {
                    var {{n.Var}} = new {{n.Entity}}
                    {
                        TenantId = {{tenant}},
            {{Block(create, 12)}}
                    };
                    if ({{n.Var}}ForCreation.Id is not null)
                        {{n.Var}}.SetId({{n.Var}}ForCreation.Id);

                    Validate{{n.Entity}}({{n.Var}});
                    {{n.Var}}.QueueDomainEvent(new {{n.Entity}}Created({{n.Var}}.Id));

                    return {{n.Var}};
                }

                public {{n.Entity}} Update({{n.Entity}}ForUpdate {{n.Var}}ForUpdate)
                {
                    GuardVersion({{n.Var}}ForUpdate.Version);
                    if (HasValuesOf({{n.Var}}ForUpdate))
                        return this;

            {{Block(update, 8)}}

                    Validate{{n.Entity}}(this);
                    QueueDomainEvent(new {{n.Entity}}Updated(Id));

                    return this;
                }

                public bool HasValuesOf({{n.Entity}}ForUpdate {{n.Var}}ForUpdate)
                    => {{same}};

                private static void Validate{{n.Entity}}({{n.Entity}} {{n.Var}})
                {
                    ValidationException.Must(EntityId.IsValid({{n.Var}}.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
            {{Block(rules, 8)}}
                }

                protected {{n.Entity}}() : base(IdPrefix) { } // For EF Core
            }
            """;
    }

    private static IEnumerable<string> Rules(Spec s, Field f, string target)
    {
        var value = $"{target}.{f.Name}";
        var longer = $"""
            ValidationException.ThrowWhenLongerThan({value}, {f.Name}MaxLength, "{f.Json}",
                $"{TooLong(s, f, "")}");
            """;
        var character = $"""
            ValidationException.ThrowWhenHasCharacterPostgresCannotStore({value}, "{f.Json}",
                "{BadCharacter(s, f)}");
            """;
        return f.Kind switch
        {
            Kind.Text when f.Optional =>
            [
                $"if ({value} is not null)",
                "{",
                Block([longer, character], 4),
                "}",
            ],
            Kind.Text => [$"ValidationException.ThrowWhenNullOrWhitespace({value}, \"{f.Json}\", \"{Missing(s, f)}\");", longer, character],
            Kind.Int =>
            [
                $"""
                ValidationException.Must({value} is >= {f.Name}Minimum and <= {f.Name}Maximum, "{f.Json}",
                    $"{OutOfRange(s, f, "")}");
                """,
            ],
            Kind.Date or Kind.ZonedDate or Kind.Instant => [$"ValidationException.MustNot({value} == default, \"{f.Json}\", \"{Missing(s, f)}\");"],
            Kind.Bool or Kind.TimeZone or Kind.ParentId => [],
        };
    }

    public static string Event(Spec s, string what) => $$"""
        namespace {{s.Names.Ns}}.DomainEvents;

        public sealed record {{s.Names.Entity}}{{what}}(string {{s.Names.Entity}}Id) : DomainEvent;
        """;

    public static string ForCreation(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Models;

        public sealed record {{s.Names.Entity}}ForCreation
        {
            /// <summary>A fixed ID with the prefix of the entity. When it is null, the entity makes a new ID.</summary>
            public string? Id { get; init; }
        {{(s.Parent is null ? "    public string TenantId { get; init; } = default!;\n" : "")}}{{Block(s.CreationFields.Select(ModelProperty), 4)}}
        }
        """;

    public static string ForUpdate(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Models;

        public sealed record {{s.Names.Entity}}ForUpdate
        {
        {{Block(s.Updatable.Select(ModelProperty), 4)}}
            public uint Version { get; init; }
        }
        """;

    public static string Dto(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Dtos;

        public sealed record {{s.Names.Entity}}Dto
        {
            public string Id { get; init; } = default!;
        {{Block(s.Fields.Select(ModelProperty), 4)}}
            public uint Version { get; init; }
        }
        """;

    private static string RequestProperty(Spec s, Field f)
    {
        var e = s.Names.Entity;
        return f.Kind switch
        {
            Kind.Text when f.Optional => $"[SchemaMaxLength(typeof({e}), nameof({e}.{f.Name}MaxLength))]\npublic string? {f.Name} {{ get; init; }}",
            Kind.Text => $"[RequiredField]\n[SchemaMaxLength(typeof({e}), nameof({e}.{f.Name}MaxLength))]\npublic string {f.Name} {{ get; init; }} = default!;",
            Kind.Int => $"[SchemaMinimum(typeof({e}), nameof({e}.{f.Name}Minimum))]\n[SchemaMaximum(typeof({e}), nameof({e}.{f.Name}Maximum))]\npublic required int {f.Name} {{ get; init; }}",
            Kind.ParentId => $"[RequiredField]\n[SchemaMaxLength(typeof(EntityId), nameof(EntityId.MaxLength))]\npublic string {f.Name} {{ get; init; }} = default!;",
            Kind.TimeZone => $"[RequiredField]\n[SchemaMaxLength(typeof(TimeZoneId), nameof(TimeZoneId.MaxLength))]\npublic string {f.Name} {{ get; init; }} = default!;",
            _ => $"public required {f.Type} {f.Name} {{ get; init; }}",
        };
    }

    public static string ForCreationDto(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Dtos;

        {{Usings(["Resources", s.HasZone ? "TimeZones" : null])}}

        public sealed record {{s.Names.Entity}}ForCreationDto
        {
        {{Block([string.Join("\n\n", s.Fields.Select(f => RequestProperty(s, f)))], 4)}}
        }
        """;

    public static string ForUpdateDto(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Dtos;

        using Resources;

        public sealed record {{s.Names.Entity}}ForUpdateDto
        {
        {{Block([string.Join("\n\n", s.Updatable.Select(f => RequestProperty(s, f)))], 4)}}

            /// <summary>The version of the {{s.Names.Words}} that the caller loaded. A stale version gives 409.</summary>
            public required uint Version { get; init; }
        }
        """;

    public static string ParametersDto(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Dtos;

        using Resources;

        public sealed class {{s.Names.Entity}}ParametersDto : BasePaginationParameters
        {
            public const int SearchMaxLength = {{s.Names.Entity}}.{{s.Title.Name}}MaxLength;

            public string? Filters { get; set; }

            public string? SortOrder { get; set; }

            public string? Search { get; set; }

            public static {{s.Names.Entity}}ParametersDto From(
                int? pageNumber, int? pageSize, string? filters, string? sortOrder, string? search)
            {
                var parameters = new {{s.Names.Entity}}ParametersDto
                {
                    Filters = string.IsNullOrEmpty(filters) ? null : filters,
                    SortOrder = string.IsNullOrEmpty(sortOrder) ? null : sortOrder,
                    Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                };
                parameters.SetPage(pageNumber, pageSize);
                return parameters;
            }
        }
        """;

    public static string Mapper(Spec s)
    {
        var (e, v) = (s.Names.Entity, s.Names.Var);
        var toCreation = s.Ref is { } reference
            ? $"[MapperIgnoreTarget(nameof({e}ForCreation.Id))]\n[MapperIgnoreSource(nameof({e}ForCreationDto.{reference.Name}))]\npublic static partial {e}ForCreation To{e}ForCreation(this {e}ForCreationDto dto);"
            : $"[MapperIgnoreTarget(nameof({e}ForCreation.Id))]\npublic static partial {e}ForCreation To{e}ForCreation(this {e}ForCreationDto dto, string tenantId);";
        var zone = s.HasZone ? "\n\n    private static string MapTimeZone(TimeZoneId timeZone) => timeZone.Value;" : "";
        return $$"""
            namespace {{s.Names.Ns}}.Mappings;

            {{Usings(["Dtos", "Models", "Riok.Mapperly.Abstractions", s.HasZone ? "TimeZones" : null])}}

            [Mapper]
            public static partial class {{e}}Mapper
            {
                [MapperIgnoreSource(nameof({{e}}.TenantId))]
                [MapperIgnoreSource(nameof({{e}}.LastModifiedOn))]
                [MapperIgnoreSource(nameof({{e}}.IsDeleted))]
                [MapperIgnoreSource(nameof({{e}}.DomainEvents))]
                public static partial {{e}}Dto To{{e}}Dto(this {{e}} {{v}});

                public static partial IQueryable<{{e}}Dto> To{{e}}DtoQueryable(this IQueryable<{{e}}> queryable);

            {{Block([toCreation], 4)}}

                public static partial {{e}}ForUpdate To{{e}}ForUpdate(this {{e}}ForUpdateDto dto);{{zone}}
            }
            """;
    }

    public static string Configuration(Spec s)
    {
        var e = s.Names.Entity;
        var columns = s.Fields.Select(f => f.Kind switch
        {
            Kind.Text when f.Optional => $"builder.Property(e => e.{f.Name})\n    .HasMaxLength({e}.{f.Name}MaxLength);",
            Kind.Text => $"builder.Property(e => e.{f.Name})\n    .IsRequired()\n    .HasMaxLength({e}.{f.Name}MaxLength);",
            Kind.ParentId => $$"""
                builder.Property(e => e.{{f.Name}})
                    .IsRequired()
                    .HasMaxLength(EntityId.MaxLength)
                    .UseCollation(EntityId.Collation);

                builder.HasOne<{{s.Parent!.Entity}}>()
                    .WithMany()
                    .HasForeignKey(e => e.{{f.Name}})
                    .OnDelete(DeleteBehavior.ClientNoAction);
                """,
            Kind.TimeZone => $$"""
                builder.ComplexProperty(e => e.{{f.Name}}, timeZone =>
                {
                    timeZone.Property(t => t.Value)
                        .HasColumnName("{{f.Snake}}")
                        .HasMaxLength(TimeZoneId.MaxLength)
                        .IsRequired();
                });
                """,
            _ => null,
        }).OfType<string>();
        var usings = Usings(["Domain", $"Domain.{s.Names.Plural}", "Domain.Tenants", s.HasZone ? "Domain.TimeZones" : null,
            s.Parent?.Namespace, "Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Metadata.Builders"]);
        return $$"""
            namespace {{s.Names.Server}}.Databases.EntityConfigurations;

            {{usings}}

            public sealed class {{e}}Configuration : IEntityTypeConfiguration<{{e}}>
            {
                public void Configure(EntityTypeBuilder<{{e}}> builder)
                {
                    builder.HasKey(e => e.Id);
                    builder.Property(e => e.Id)
                        .HasMaxLength(EntityId.MaxLength)
                        .UseCollation(EntityId.Collation);

                    builder.Property(e => e.TenantId)
                        .IsRequired()
                        .HasMaxLength(EntityId.MaxLength)
                        .UseCollation(EntityId.Collation);

                    builder.HasOne<Tenant>()
                        .WithMany()
                        .HasForeignKey(e => e.TenantId)
                        .OnDelete(DeleteBehavior.ClientNoAction);

            {{Block([string.Join("\n\n", columns)], 8)}}

                    builder.HasIndex(e => new { e.TenantId, e.{{s.Title.Name}}, e.Id });
                }
            }
            """;
    }

    public static string AddFeature(Spec s)
    {
        var (n, v) = (s.Names, s.Names.Var);
        var start = new List<string>();
        var body = new List<string>();
        if (s.Parent is { } p && s.Ref is { } r)
        {
            start.AddRange([$"Activity.Current?.SetTag(\"app.{p.Snake}_id\", request.Dto.{r.Name});", ""]);
            body.AddRange([
                $$"""
                // A malformed ID matches no row, and Postgres cannot read some of them, such as one with U+0000
                var {{p.Var}} = EntityId.IsValid(request.Dto.{{r.Name}}, {{p.Entity}}.IdPrefix)
                    ? await dbContext.{{p.Plural}}
                        .TagWithOperationCallSite("{{n.SnakePlural}}.add.load_{{p.Snake}}")
                        .FirstOrDefaultAsync(x => x.Id == request.Dto.{{r.Name}}, ct)
                    : null;
                if ({{p.Var}} is null)
                    throw new ValidationException("{{r.Json}}", "{{EscapeForCs(Texts.ParentMissing(r))}}");
                """,
                "",
                $"var {v} = {n.Entity}.Create({p.Var}, request.Dto.To{n.Entity}ForCreation());",
            ]);
        }
        else
        {
            body.Add($"var {v} = {n.Entity}.Create(request.Dto.To{n.Entity}ForCreation(tenantId));");
        }
        start.Add($"await permissionGuard.RequireAsync(Permission.{n.Permission}(), cancellationToken);");
        body.AddRange([
            $"Activity.Current?.SetTag(\"app.{n.Snake}_id\", {v}.Id);",
            "",
            $"await dbContext.{n.Plural}.AddAsync({v}, ct);",
            "await dbContext.SaveChangesAsync(ct);",
            $"return {v};",
        ]);
        var usings = Usings(["System.Diagnostics", "Databases", "Dtos", "Exceptions", "Mappings", "MediatR",
            "Microsoft.EntityFrameworkCore", "Resources", "Serilog", "Users", s.Parent?.Namespace]);
        return $$"""
            namespace {{n.Ns}}.Features;

            {{usings}}

            public static class Add{{n.Entity}}
            {
                public sealed record Command({{n.Entity}}ForCreationDto Dto) : IRequest<{{n.Entity}}Dto>;

                public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
                    : IRequestHandler<Command, {{n.Entity}}Dto>
                {
                    private readonly ILogger _logger = Log.ForContext<Handler>();

                    public async Task<{{n.Entity}}Dto> Handle(Command request, CancellationToken cancellationToken)
                    {
            {{Block(start, 12)}}

                        var tenantId = dbContext.CurrentTenantScope.TenantId
                            ?? throw new ForbiddenAccessException("{{EscapeForCs(Texts.AddToOtherTenant(s))}}");
                        Activity.Current?.SetTag("app.tenant_id", tenantId);

                        // The shared lock keeps a tenant delete out until the save, so the {{n.Words}} does not land in a deleted tenant.
                        var added = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
                        {
                            var tenantExists = await dbContext.Tenants
                                .TagWithOperationCallSite("{{n.SnakePlural}}.add.tenant_exists")
                                .AnyAsync(t => t.Id == tenantId, ct);
                            if (!tenantExists)
                                throw new NotFoundException("The tenant does not exist.");

            {{Block(body, 16)}}
                        }, cancellationToken);

                        _logger.Here().Information("Added {{n.Words}} {{{n.Entity}}Id}", added.Id);

                        return added.To{{n.Entity}}Dto();
                    }
                }
            }
            """;
    }

    public static string GetFeature(Spec s)
    {
        var n = s.Names;
        return $$"""
            namespace {{n.Ns}}.Features;

            using System.Diagnostics;
            using Databases;
            using Dtos;
            using Mappings;
            using MediatR;

            public static class Get{{n.Entity}}
            {
                public sealed record Query(string Id) : IRequest<{{n.Entity}}Dto>;

                public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, {{n.Entity}}Dto>
                {
                    public async Task<{{n.Entity}}Dto> Handle(Query request, CancellationToken cancellationToken)
                    {
                        Activity.Current?.SetTag("app.{{n.Snake}}_id", request.Id);

                        var {{n.Var}} = await dbContext.{{n.Plural}}
                            .TagWithOperationCallSite("{{n.SnakePlural}}.get_by_id")
                            .GetById(request.Id, cancellationToken);
                        return {{n.Var}}.To{{n.Entity}}Dto();
                    }
                }
            }
            """;
    }

    public static string ListFeature(Spec s)
    {
        var n = s.Names;
        return $$"""
            namespace {{n.Ns}}.Features;

            using Databases;
            using Dtos;
            using Exceptions;
            using Mappings;
            using MediatR;
            using Microsoft.EntityFrameworkCore;
            using Resources;

            public static class Get{{n.Entity}}List
            {
                public sealed record Query({{n.Entity}}ParametersDto Parameters) : IRequest<PagedList<{{n.Entity}}Dto>>;

                public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<{{n.Entity}}Dto>>
                {
                    private const string SearchParameter = "search";

                    public async Task<PagedList<{{n.Entity}}Dto>> Handle(Query request, CancellationToken cancellationToken)
                    {
                        var queryKitConfig = new CustomQueryKitConfiguration();

                        var rows = dbContext.{{n.Plural}}
                            .TagWithOperationCallSite("{{n.SnakePlural}}.list")
                            .AsNoTracking();

                        if (request.Parameters.Search is { } search)
                        {
                            if (search.Length > {{n.Entity}}ParametersDto.SearchMaxLength)
                                throw new QueryInputException(SearchParameter,
                                    $"A search can have no more than {{{n.Entity}}ParametersDto.SearchMaxLength} characters.");
                            if (!PostgresText.CanStore(search))
                                throw new QueryInputException(SearchParameter, "The search has a character that is not allowed.");

                            var pattern = LikePattern.Containing(search);
                            rows = rows.Where(x => EF.Functions.ILike(x.{{s.Title.Name}}, pattern, LikePattern.EscapeCharacter));
                        }

                        var query = rows.To{{n.Entity}}DtoQueryable();

                        if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                            query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

                        query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                            ? query.OrderBy(x => x.{{s.Title.Name}}).ThenBy(x => x.Id)
                            : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(x => x.Id);

                        return await PagedList.CreateAsync(
                            query,
                            request.Parameters.PageNumber,
                            request.Parameters.PageSize,
                            cancellationToken);
                    }
                }
            }
            """;
    }

    public static string UpdateFeature(Spec s)
    {
        var n = s.Names;
        return $$"""
            namespace {{n.Ns}}.Features;

            using System.Diagnostics;
            using Databases;
            using Dtos;
            using Mappings;
            using MediatR;
            using Resources;
            using Serilog;
            using Users;

            public static class Update{{n.Entity}}
            {
                public sealed record Command(string Id, {{n.Entity}}ForUpdateDto Dto) : IRequest<{{n.Entity}}Dto>;

                public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
                    : IRequestHandler<Command, {{n.Entity}}Dto>
                {
                    private readonly ILogger _logger = Log.ForContext<Handler>();

                    public async Task<{{n.Entity}}Dto> Handle(Command request, CancellationToken cancellationToken)
                    {
                        Activity.Current?.SetTag("app.{{n.Snake}}_id", request.Id);

                        await permissionGuard.RequireAsync(Permission.{{n.Permission}}(), cancellationToken);

                        var {{n.Var}} = await dbContext.{{n.Plural}}
                            .TagWithOperationCallSite("{{n.SnakePlural}}.update.load")
                            .GetById(request.Id, cancellationToken);

                        var {{n.Var}}ForUpdate = request.Dto.To{{n.Entity}}ForUpdate();
                        var changes = !{{n.Var}}.HasValuesOf({{n.Var}}ForUpdate);
                        {{n.Var}}.Update({{n.Var}}ForUpdate);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        if (changes)
                            _logger.Here().Information("Updated {{n.Words}} {{{n.Entity}}Id}", {{n.Var}}.Id);

                        return {{n.Var}}.To{{n.Entity}}Dto();
                    }
                }
            }
            """;
    }

    public static string DeleteFeature(Spec s)
    {
        var n = s.Names;
        return $$"""
            namespace {{n.Ns}}.Features;

            using System.Diagnostics;
            using Databases;
            using MediatR;
            using Resources;
            using Serilog;
            using Users;

            public static class Delete{{n.Entity}}
            {
                public sealed record Command(string Id) : IRequest, IRunsAgainWhenRaced;

                public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
                {
                    private readonly ILogger _logger = Log.ForContext<Handler>();

                    public async Task Handle(Command request, CancellationToken cancellationToken)
                    {
                        Activity.Current?.SetTag("app.{{n.Snake}}_id", request.Id);

                        await permissionGuard.RequireAsync(Permission.{{n.Permission}}(), cancellationToken);

                        var {{n.Var}} = await dbContext.{{n.Plural}}
                            .TagWithOperationCallSite("{{n.SnakePlural}}.delete.load")
                            .GetById(request.Id, cancellationToken);

                        dbContext.{{n.Plural}}.Remove({{n.Var}});
                        await dbContext.SaveChangesAsync(cancellationToken);

                        _logger.Here().Information("Deleted {{n.Words}} {{{n.Entity}}Id}", {{n.Var}}.Id);
                    }
                }
            }
            """;
    }

    private static string HttpHeader(Spec s, params string[] usings) => $$"""
        namespace {{s.Names.Ns}}.Features;

        {{string.Join("\n", usings.Select(u => $"using {u};"))}}
        """;

    public static string AddHttp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{HttpHeader(s, "Dtos", "MediatR", "Microsoft.AspNetCore.Http.HttpResults", "Resources.Entrypoints.Http")}}

            public sealed class Add{{n.Entity}}Endpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
                    .MapPost("/{{n.KebabPlural}}", Handle)
                    .WithName("Add{{n.Entity}}")
                    .WithTags("{{n.Plural}}")
                    .MapToApiVersion(ApiVersions.V1)
                    .RequireAuthorization()
                    .ProducesValidationProblem()
                    .ProducesProblem(StatusCodes.Status403Forbidden)
                    .ProducesProblem(StatusCodes.Status404NotFound)
                    .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

                /// <summary>
                /// Adds {{n.A}} {{n.Words}} to the tenant of the caller.
                /// </summary>
                public static async Task<CreatedAtRoute<{{n.Entity}}Dto>> Handle(
                    {{n.Entity}}ForCreationDto dto,
                    IMediator mediator,
                    CancellationToken cancellationToken)
                {
                    var result = await mediator.Send(new Add{{n.Entity}}.Command(dto), cancellationToken);
                    return TypedResults.CreatedAtRoute(result, "Get{{n.Entity}}", new { id = result.Id });
                }
            }
            """;
    }

    public static string GetHttp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{HttpHeader(s, "Dtos", "MediatR", "Microsoft.AspNetCore.Http.HttpResults", "Resources.Entrypoints.Http")}}

            public sealed class Get{{n.Entity}}Endpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
                    .MapGet("/{{n.KebabPlural}}/{id}", Handle)
                    .WithName("Get{{n.Entity}}")
                    .WithTags("{{n.Plural}}")
                    .MapToApiVersion(ApiVersions.V1)
                    .RequireAuthorization()
                    .ProducesProblem(StatusCodes.Status404NotFound);

                /// <summary>
                /// Gets one {{n.Words}} by ID.
                /// </summary>
                public static async Task<Ok<{{n.Entity}}Dto>> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
                {
                    var result = await mediator.Send(new Get{{n.Entity}}.Query(id), cancellationToken);
                    return TypedResults.Ok(result);
                }
            }
            """;
    }

    public static string ListHttp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{HttpHeader(s, "System.ComponentModel.DataAnnotations", "Dtos", "MediatR", "Microsoft.AspNetCore.Http.HttpResults", "Resources", "Resources.Entrypoints.Http")}}

            public sealed class Get{{n.Entity}}ListEndpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
                    .MapGet("/{{n.KebabPlural}}", Handle)
                    .WithName("Get{{n.Entity}}List")
                    .WithTags("{{n.Plural}}")
                    .MapToApiVersion(ApiVersions.V1)
                    .RequireAuthorization()
                    .ProducesValidationProblem();

                /// <summary>
                /// Gets a page of {{n.WordsPlural}}. The search finds the {{n.WordsPlural}} whose "{{s.Title.Label}}" value contains the text.
                /// </summary>
                public static async Task<Ok<PagedList<{{n.Entity}}Dto>>> Handle(
                    [MaxLength(CustomQueryKitConfiguration.FilterMaxLength)] string? filters,
                    string? sortOrder,
                    [MaxLength({{n.Entity}}ParametersDto.SearchMaxLength)] string? search,
                    [Range(1, BasePaginationParameters.MaxPageNumber)] int? pageNumber,
                    [Range(1, int.MaxValue)] int? pageSize,
                    IMediator mediator,
                    CancellationToken cancellationToken)
                {
                    var parameters = {{n.Entity}}ParametersDto.From(pageNumber, pageSize, filters, sortOrder, search);

                    var result = await mediator.Send(new Get{{n.Entity}}List.Query(parameters), cancellationToken);
                    return TypedResults.Ok(result);
                }
            }
            """;
    }

    public static string UpdateHttp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{HttpHeader(s, "Dtos", "MediatR", "Microsoft.AspNetCore.Http.HttpResults", "Resources.Entrypoints.Http")}}

            public sealed class Update{{n.Entity}}Endpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
                    .MapPut("/{{n.KebabPlural}}/{id}", Handle)
                    .WithName("Update{{n.Entity}}")
                    .WithTags("{{n.Plural}}")
                    .MapToApiVersion(ApiVersions.V1)
                    .RequireAuthorization()
                    .ProducesProblem(StatusCodes.Status403Forbidden)
                    .ProducesProblem(StatusCodes.Status404NotFound)
                    .ProducesValidationProblem()
                    .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
                    .ProducesValidationProblem(StatusCodes.Status409Conflict);

                /// <summary>
                /// Updates {{n.A}} {{n.Words}}.
                /// </summary>
                public static async Task<Ok<{{n.Entity}}Dto>> Handle(
                    string id,
                    {{n.Entity}}ForUpdateDto dto,
                    IMediator mediator,
                    CancellationToken cancellationToken)
                {
                    var result = await mediator.Send(new Update{{n.Entity}}.Command(id, dto), cancellationToken);
                    return TypedResults.Ok(result);
                }
            }
            """;
    }

    public static string DeleteHttp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{HttpHeader(s, "MediatR", "Microsoft.AspNetCore.Http.HttpResults", "Resources.Entrypoints.Http")}}

            public sealed class Delete{{n.Entity}}Endpoint : IEndpoint
            {
                public void Map(IEndpointRouteBuilder versionedGroup) => versionedGroup
                    .MapDelete("/{{n.KebabPlural}}/{id}", Handle)
                    .WithName("Delete{{n.Entity}}")
                    .WithTags("{{n.Plural}}")
                    .MapToApiVersion(ApiVersions.V1)
                    .RequireAuthorization()
                    .ProducesProblem(StatusCodes.Status403Forbidden)
                    .ProducesProblem(StatusCodes.Status404NotFound)
                    .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
                    .ProducesValidationProblem(StatusCodes.Status409Conflict);

                /// <summary>
                /// Deletes {{n.A}} {{n.Words}}.
                /// </summary>
                public static async Task<NoContent> Handle(string id, IMediator mediator, CancellationToken cancellationToken)
                {
                    await mediator.Send(new Delete{{n.Entity}}.Command(id), cancellationToken);
                    return TypedResults.NoContent();
                }
            }
            """;
    }

    private static string McpHeader(Spec s, string feature, params string[] usings) => $$"""
        namespace {{s.Names.Ns}}.Features;

        {{string.Join("\n", usings.Select(u => $"using {u};"))}}

        /// <summary>
        /// The MCP tool of <see cref="{{feature}}"/>. The SDK resolves <see cref="IMediator"/> from the request services,
        /// so it is not in the input schema. <c>McpDomainErrors</c> maps the domain exceptions to tool errors.
        /// </summary>
        """;

    private static string ToolParameter(Spec s, Field f)
    {
        var description = $"[Description(\"{EscapeForCs(Texts.ToolParameter(s, f))}\")]";
        return f.Optional ? $"{description} string? {f.Camel} = null," : $"{description} {f.Type} {f.Camel},";
    }

    private static List<string> ToolParameters(Spec s, bool forUpdate)
    {
        var fields = forUpdate ? s.Updatable.ToList() : s.Fields.ToList();
        var lines = new List<string> { "IMediator mediator," };
        if (forUpdate)
            lines.Add($"[Description(\"The {s.Names.Words} ID, for example {s.Names.Prefix}_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f.\")] string id,");
        lines.AddRange(fields.Where(f => !f.Optional).Select(f => ToolParameter(s, f)));
        if (forUpdate)
            lines.Add($"[Description(\"The version of the {s.Names.Words} that get_{s.Names.Snake} gave. If the {s.Names.Words} changed after that, the tool gives the aborted error.\")] uint version,");
        lines.Add("CancellationToken cancellationToken,");
        lines.AddRange(fields.Where(f => f.Optional).Select(f => ToolParameter(s, f)));
        lines[^1] = lines[^1].TrimEnd(',');
        return lines;
    }

    private static string ToolDto(Spec s, string dto, bool forUpdate)
    {
        var values = (forUpdate ? s.Updatable : s.Fields).Select(f => $"{f.Name} = {f.Camel},").ToList();
        if (forUpdate)
            values.Add("Version = version,");
        return $$"""
            new {{dto}}
            {
            {{Block(values, 4)}}
            }
            """;
    }

    public static string AddMcp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{McpHeader(s, $"Add{n.Entity}", "System.ComponentModel", "Domain.Users", "Dtos", "MediatR", "ModelContextProtocol.Server", "Resources.Entrypoints.Mcp")}}
            [McpServerToolType]
            public static class Add{{n.Entity}}Tool
            {
                [McpServerTool(Name = "add_{{n.Snake}}", Destructive = false, UseStructuredContent = true)]
                [ToolAccess(Permission.Names.{{n.Permission}})]
                [Description("{{EscapeForCs(Texts.AddTool(s))}}")]
                public static Task<{{n.Entity}}Dto> Handle(
            {{Block(ToolParameters(s, forUpdate: false), 8)}})
                    => mediator.Send(new Add{{n.Entity}}.Command(
            {{Block([ToolDto(s, $"{n.Entity}ForCreationDto", forUpdate: false)], 12)}}),
                        cancellationToken);
            }
            """;
    }

    public static string GetMcp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{McpHeader(s, $"Get{n.Entity}", "System.ComponentModel", "Dtos", "MediatR", "ModelContextProtocol.Server", "Resources.Entrypoints.Mcp")}}
            [McpServerToolType]
            public static class Get{{n.Entity}}Tool
            {
                [McpServerTool(Name = "get_{{n.Snake}}", ReadOnly = true, UseStructuredContent = true)]
                [ToolAccess(ToolAccessKind.Read)]
                [Description("{{EscapeForCs(Texts.GetTool(s))}}")]
                public static Task<{{n.Entity}}Dto> Handle(
                    IMediator mediator,
                    [Description("The {{n.Words}} ID, for example {{n.Prefix}}_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f.")] string id,
                    CancellationToken cancellationToken)
                    => mediator.Send(new Get{{n.Entity}}.Query(id), cancellationToken);
            }
            """;
    }

    public static string ListMcp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{McpHeader(s, $"Get{n.Entity}List", "System.ComponentModel", "Dtos", "MediatR", "ModelContextProtocol.Server", "Resources.Entrypoints.Mcp")}}
            [McpServerToolType]
            public static class Get{{n.Entity}}ListTool
            {
                [McpServerTool(Name = "list_{{n.SnakePlural}}", ReadOnly = true, UseStructuredContent = true)]
                [ToolAccess(ToolAccessKind.Read)]
                [Description("{{EscapeForCs(Texts.ListTool(s))}}")]
                public static async Task<McpPage<{{n.Entity}}Dto>> Handle(
                    IMediator mediator,
                    CancellationToken cancellationToken,
                    [Description("{{EscapeForCs(Texts.Search(s))}}")] string? search = null,
                    [Description("The page number. The first page is 1. Leave it out for the first page.")] int? pageNumber = null,
                    [Description("The number of {{n.WordsPlural}} on a page, at least 1. Leave it out for the default size. A value more than the maximum gives the maximum. The result gives the size that was used.")] int? pageSize = null,
                    [Description("{{EscapeForCs(Texts.Filters(s))}}")] string? filters = null,
                    [Description("A QueryKit sort order, for example: -{{s.Title.Json}}")] string? sortOrder = null)
                {
                    var parameters = {{n.Entity}}ParametersDto.From(pageNumber, pageSize, filters, sortOrder, search);
                    var result = await mediator.Send(new Get{{n.Entity}}List.Query(parameters), cancellationToken);
                    return McpPage.From(result);
                }
            }
            """;
    }

    public static string UpdateMcp(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{McpHeader(s, $"Update{n.Entity}", "System.ComponentModel", "Domain.Users", "Dtos", "MediatR", "ModelContextProtocol.Server", "Resources.Entrypoints.Mcp")}}
            [McpServerToolType]
            public static class Update{{n.Entity}}Tool
            {
                // An update overwrites the values, so the tool is not additive: the MCP spec says destructive.
                [McpServerTool(Name = "update_{{n.Snake}}", Destructive = true, Idempotent = true, UseStructuredContent = true)]
                [ToolAccess(Permission.Names.{{n.Permission}})]
                [Description("{{EscapeForCs(Texts.UpdateTool(s))}}")]
                public static Task<{{n.Entity}}Dto> Handle(
            {{Block(ToolParameters(s, forUpdate: true), 8)}})
                    => mediator.Send(new Update{{n.Entity}}.Command(id,
            {{Block([ToolDto(s, $"{n.Entity}ForUpdateDto", forUpdate: true)], 12)}}),
                        cancellationToken);
            }
            """;
    }

    private static string ProtoType(Field f) => f.Kind switch
    {
        Kind.Text => f.Optional ? "optional string" : "string",
        Kind.ParentId => "string",
        Kind.Int => "int32",
        Kind.Bool => "bool",
        Kind.Instant => "google.protobuf.Timestamp",
        _ => throw new FeatureException("Protobuf has no date type."),
    };

    private static IEnumerable<string> ProtoFields(IEnumerable<Field> fields, int first)
        => fields.Select((f, i) => $"{ProtoType(f)} {f.Snake} = {first + i};");

    public static string Proto(Spec s)
    {
        var n = s.Names;
        var import = s.Fields.Any(f => f.Kind == Kind.Instant) ? "\nimport \"google/protobuf/timestamp.proto\";\n" : "";
        return $$"""
            syntax = "proto3";

            // The package and the C# namespace carry the version, the same as the /api/v1 segment of the HTTP API.
            package {{n.GrpcPackage}};
            {{import}}
            option csharp_namespace = "{{n.Ns}}.Grpc.V1";

            // The {{n.WordsPlural}} of the tenant of the caller. Each call needs a JWT bearer token in the authorization metadata,
            // and it sees the same tenant scope as the HTTP API.
            service {{n.GrpcService}} {
              // Gets one {{n.Words}}. NOT_FOUND when the caller cannot see {{n.A}} {{n.Words}} with the ID.
              rpc Get{{n.Entity}}(Get{{n.Entity}}Request) returns (Get{{n.Entity}}Response);

              // Gets one page of {{n.WordsPlural}}. INVALID_ARGUMENT for a bad search, filter, sort order, or page.
              rpc List{{n.Plural}}(List{{n.Plural}}Request) returns (List{{n.Plural}}Response);

              // Adds {{n.A}} {{n.Words}}. PERMISSION_DENIED without the {{n.PermissionName}} permission.
              rpc Add{{n.Entity}}(Add{{n.Entity}}Request) returns (Add{{n.Entity}}Response);

              // Updates {{n.A}} {{n.Words}}. ABORTED when the version is stale.
              rpc Update{{n.Entity}}(Update{{n.Entity}}Request) returns (Update{{n.Entity}}Response);

              // Deletes {{n.A}} {{n.Words}}. NOT_FOUND when the caller cannot see {{n.A}} {{n.Words}} with the ID.
              rpc Delete{{n.Entity}}(Delete{{n.Entity}}Request) returns (Delete{{n.Entity}}Response);
            }

            message {{n.Entity}} {
              string id = 1;
            {{Block(ProtoFields(s.Fields, 2), 2)}}
              uint32 version = {{s.Fields.Count + 2}};
            }

            message Get{{n.Entity}}Request {
              string id = 1;
            }

            message Get{{n.Entity}}Response {
              {{n.Entity}} {{n.Snake}} = 1;
            }

            // Each field is optional. An unset field uses the default of the HTTP API.
            message List{{n.Plural}}Request {
              optional int32 page_number = 1;
              optional int32 page_size = 2;
              optional string filters = 3;
              optional string sort_order = 4;
              // {{Texts.Search(s)}}
              optional string search = 5;
            }

            message List{{n.Plural}}Response {
              repeated {{n.Entity}} {{n.SnakePlural}} = 1;
              Pagination pagination = 2;
            }

            // The same data as the X-Pagination header of the HTTP API.
            message Pagination {
              int32 page_number = 1;
              int32 page_size = 2;
              int32 current_page_size = 3;
              int32 total_count = 4;
              int32 total_pages = 5;
              bool has_previous = 6;
              bool has_next = 7;
            }

            message Add{{n.Entity}}Request {
            {{Block(ProtoFields(s.Fields, 1), 2)}}
            }

            message Add{{n.Entity}}Response {
              {{n.Entity}} {{n.Snake}} = 1;
            }

            message Update{{n.Entity}}Request {
              string id = 1;
            {{Block(ProtoFields(s.Updatable, 2), 2)}}
              uint32 version = {{s.Updatable.Count() + 2}};
            }

            message Update{{n.Entity}}Response {
              {{n.Entity}} {{n.Snake}} = 1;
            }

            message Delete{{n.Entity}}Request {
              string id = 1;
            }

            message Delete{{n.Entity}}Response {}
            """;
    }

    public static string GrpcService(Spec s) => $$"""
        namespace {{s.Names.Ns}}.Grpc.V1;

        using MediatR;
        using Microsoft.AspNetCore.Authorization;

        /// <summary>
        /// The gRPC v1 adapter of the {{s.Names.Words}} features. Each RPC is a partial method in the folder of its feature.
        /// <c>DomainErrorInterceptor</c> maps the domain exceptions to gRPC status codes.
        /// </summary>
        [Authorize]
        public sealed partial class {{s.Names.Plural}}GrpcService(IMediator mediator) : {{s.Names.GrpcService}}.{{s.Names.GrpcService}}Base;
        """;

    private static bool HasInstant(Spec s) => s.Fields.Any(f => f.Kind == Kind.Instant);

    public static string GrpcMappings(Spec s)
    {
        var n = s.Names;
        var values = s.Fields.Where(f => !f.Optional).Select(f => f.Kind == Kind.Instant
            ? $"{f.Name} = Timestamp.FromDateTimeOffset(dto.{f.Name}),"
            : $"{f.Name} = dto.{f.Name},");
        var optional = s.Fields.Where(f => f.Optional).Select(f => $"if (dto.{f.Name} is not null)\n    message.{f.Name} = dto.{f.Name};");
        var usings = HasInstant(s) ? "using Dtos;\nusing global::Google.Protobuf.WellKnownTypes;\nusing Resources;" : "using Dtos;\nusing Resources;";
        return $$"""
            namespace {{n.Ns}}.Grpc.V1;

            {{usings}}

            /// <summary>Maps the {{n.Words}} DTOs to the gRPC v1 messages. More than one RPC uses each map.</summary>
            public static class {{n.Entity}}GrpcMappings
            {
                public static {{n.Entity}} ToMessage(this {{n.Entity}}Dto dto)
                {
                    var message = new {{n.Entity}}
                    {
                        Id = dto.Id,
            {{Block(values, 12)}}
                        Version = dto.Version,
                    };
            {{Block(optional, 8)}}
                    return message;
                }

                public static Pagination ToPaginationMessage(this IPagedList pagedList) => new()
                {
                    PageNumber = pagedList.PageNumber,
                    PageSize = pagedList.PageSize,
                    CurrentPageSize = pagedList.CurrentPageSize,
                    TotalCount = pagedList.TotalCount,
                    TotalPages = pagedList.TotalPages,
                    HasPrevious = pagedList.HasPrevious,
                    HasNext = pagedList.HasNext,
                };
            }
            """.Replace("\n\n        return message;", "\n        return message;");
    }

    private static IEnumerable<string> FromRequest(IEnumerable<Field> fields) => fields.Select(f => f.Kind switch
    {
        Kind.Text when f.Optional => $"{f.Name} = request.Has{f.Name} ? request.{f.Name} : null,",
        Kind.Instant => $"{f.Name} = request.{f.Name}?.ToDateTimeOffset() ?? default,",
        _ => $"{f.Name} = request.{f.Name},",
    });

    private static string GrpcHeader(Spec s, bool dtos) => $$"""
        namespace {{s.Names.Ns}}.Grpc.V1;

        // "Grpc" alone would bind to the Grpc segment of this namespace, so the gRPC library needs the global alias.
        using global::Grpc.Core;
        {{(dtos ? "using Dtos;\n" : "")}}using Features;
        """;

    public static string AddGrpc(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{GrpcHeader(s, dtos: true)}}

            public sealed partial class {{n.Plural}}GrpcService
            {
                public override async Task<Add{{n.Entity}}Response> Add{{n.Entity}}(Add{{n.Entity}}Request request, ServerCallContext context)
                {
                    var dto = new {{n.Entity}}ForCreationDto
                    {
            {{Block(FromRequest(s.Fields), 12)}}
                    };
                    var result = await mediator.Send(new Add{{n.Entity}}.Command(dto), context.CancellationToken);
                    return new Add{{n.Entity}}Response { {{n.Entity}} = result.ToMessage() };
                }
            }
            """;
    }

    public static string GetGrpc(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{GrpcHeader(s, dtos: false)}}

            public sealed partial class {{n.Plural}}GrpcService
            {
                public override async Task<Get{{n.Entity}}Response> Get{{n.Entity}}(Get{{n.Entity}}Request request, ServerCallContext context)
                {
                    var result = await mediator.Send(new Get{{n.Entity}}.Query(request.Id), context.CancellationToken);
                    return new Get{{n.Entity}}Response { {{n.Entity}} = result.ToMessage() };
                }
            }
            """;
    }

    public static string ListGrpc(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{GrpcHeader(s, dtos: true)}}

            public sealed partial class {{n.Plural}}GrpcService
            {
                public override async Task<List{{n.Plural}}Response> List{{n.Plural}}(List{{n.Plural}}Request request, ServerCallContext context)
                {
                    var parameters = {{n.Entity}}ParametersDto.From(
                        request.HasPageNumber ? request.PageNumber : null,
                        request.HasPageSize ? request.PageSize : null,
                        request.HasFilters ? request.Filters : null,
                        request.HasSortOrder ? request.SortOrder : null,
                        request.HasSearch ? request.Search : null);

                    var result = await mediator.Send(new Get{{n.Entity}}List.Query(parameters), context.CancellationToken);

                    var response = new List{{n.Plural}}Response { Pagination = result.ToPaginationMessage() };
                    response.{{n.Plural}}.AddRange(result.Select(x => x.ToMessage()));
                    return response;
                }
            }
            """;
    }

    public static string UpdateGrpc(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{GrpcHeader(s, dtos: true)}}

            public sealed partial class {{n.Plural}}GrpcService
            {
                public override async Task<Update{{n.Entity}}Response> Update{{n.Entity}}(Update{{n.Entity}}Request request, ServerCallContext context)
                {
                    var dto = new {{n.Entity}}ForUpdateDto
                    {
            {{Block(FromRequest(s.Updatable), 12)}}
                        Version = request.Version,
                    };
                    var result = await mediator.Send(new Update{{n.Entity}}.Command(request.Id, dto), context.CancellationToken);
                    return new Update{{n.Entity}}Response { {{n.Entity}} = result.ToMessage() };
                }
            }
            """;
    }

    public static string DeleteGrpc(Spec s)
    {
        var n = s.Names;
        return $$"""
            {{GrpcHeader(s, dtos: false)}}

            public sealed partial class {{n.Plural}}GrpcService
            {
                public override async Task<Delete{{n.Entity}}Response> Delete{{n.Entity}}(Delete{{n.Entity}}Request request, ServerCallContext context)
                {
                    await mediator.Send(new Delete{{n.Entity}}.Command(request.Id), context.CancellationToken);
                    return new Delete{{n.Entity}}Response();
                }
            }
            """;
    }

    private static string InputType(Field f) => f.Kind switch
    {
        Kind.Text => "string?",
        _ => f.Type + "?",
    };

    public static string GraphInput(Spec s) => $$"""
        namespace {{s.Names.App}}.SharedTestHelpers.EntityGraphs;

        public sealed class {{s.Names.Entity}}Input
        {
            public string? Id { get; set; }
        {{Block(s.CreationFields.Select(f => $"\npublic {InputType(f)} {f.Name} {{ get; set; }}"), 4)}}
        }
        """;

    public static string Blueprint(Spec s)
    {
        var n = s.Names;
        var given = s.CreationFields.Select(f => f.Kind switch
        {
            Kind.Text when f.Optional => $"{f.Name} = input.{f.Name},",
            Kind.Text or Kind.TimeZone => $"{f.Name} = input.{f.Name}!,",
            _ => $"{f.Name} = input.{f.Name}!.Value,",
        });
        var derives = s.CreationFields.Select(f => $".Derive(i => i.{f.Name}, (_, _) => {n.Entity}Rules.{f.Name}(Faker))").ToList();
        derives[^1] += ";";
        var (owner, ownerVar) = s.Parent is { } p ? (p.Entity, p.Var) : ("Tenant", "tenant");
        string[] values = s.Parent is null ? ["Id = input.Id,", "TenantId = tenant.Id,", .. given] : ["Id = input.Id,", .. given];
        var first = s.Parent is null ? "" : $"{ownerVar}, ";
        var usings = Usings(["Bogus", "Core", $"Fakes.{n.Plural}", n.Ns, $"{n.Ns}.Models",
            s.Parent?.Namespace ?? $"{n.Server}.Domain.Tenants"]);
        return $$"""
            namespace {{n.App}}.SharedTestHelpers.EntityGraphs;

            {{usings}}

            [GraphGraft]
            public static class {{n.Entity}}Blueprints
            {
                private static readonly Faker Faker = new();

                [Graft(Plural = "{{n.Plural}}")]
                public static readonly ChildInputBlueprint<TestSeed, {{owner}}, {{n.Entity}}Input, {{n.Entity}}> {{n.Entity}} = Blueprint
                    .ChildFromInput<TestSeed, {{owner}}, {{n.Entity}}Input, {{n.Entity}}>(
                        () => new(),
                        ({{ownerVar}}, input, _) => Server.Domain.{{n.Plural}}.{{n.Entity}}.Create({{first}}new {{n.Entity}}ForCreation
                        {
            {{Block(values, 16)}}
                        }))
            {{Block(derives, 8)}}
            }
            """;
    }

    private static string RuleBody(Spec s, Field f)
    {
        var e = s.Names.Entity;
        return f.Kind switch
        {
            Kind.Text when f.Optional => $"Fit(f.Lorem.Sentence(), {e}.{f.Name}MaxLength)",
            Kind.Text => $"Fit(f.Commerce.ProductName(), {e}.{f.Name}MaxLength)",
            Kind.Int => $"f.Random.Int({e}.{f.Name}Minimum, {e}.{f.Name}Maximum)",
            Kind.Bool => "f.Random.Bool()",
            Kind.Date or Kind.ZonedDate => "DateOnly.FromDayNumber(f.Random.Int(738_000, 740_000))",
            Kind.TimeZone => "f.PickRandom(TimeZones)",
            Kind.Instant => "DateTimeOffset.FromUnixTimeSeconds(f.Random.Long(1_600_000_000, 1_900_000_000))",
            Kind.ParentId => throw new InvalidOperationException("A reference has no rule: the graph adds its parent."),
        };
    }

    public static string Rules(Spec s)
    {
        var n = s.Names;
        var rules = s.CreationFields.Select(f => $"public static {f.Type.TrimEnd('?')} {f.Name}(Faker f) => {RuleBody(s, f)};");
        var zones = s.HasZone
            ? "private static readonly string[] TimeZones =\n    [\"Pacific/Tongatapu\", \"Asia/Tokyo\", \"Europe/Berlin\", \"America/Chicago\", \"America/Los_Angeles\", \"Pacific/Honolulu\"];\n\n"
            : "";
        return $$"""
            namespace {{n.App}}.SharedTestHelpers.Fakes.{{n.Plural}};

            using Bogus;
            using {{n.Server}}.Domain.{{n.Plural}};

            /// <summary>
            /// The one definition of a realistic {{n.Words}}. Each method is named for the property that it fills.
            /// The shape fakers and the entity graph blueprint use these methods, so a change here reaches all of them.
            /// </summary>
            public static class {{n.Entity}}Rules
            {
            {{Block([zones + string.Join("\n\n", rules)], 4)}}

                private static string Fit(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
            }
            """;
    }

    public static string FakeForCreationDto(Spec s)
    {
        var n = s.Names;
        var rules = s.CreationFields.Select(f => $"RuleFor(x => x.{f.Name}, {n.Entity}Rules.{f.Name});").ToList();
        var parameters = "";
        if (s.Ref is { } r)
        {
            parameters = $"string {r.Camel}";
            rules.Insert(0, $"RuleFor(x => x.{r.Name}, {r.Camel});");
        }
        return $$"""
            namespace {{n.App}}.SharedTestHelpers.Fakes.{{n.Plural}};

            using AutoBogus;
            using {{n.Server}}.Domain.{{n.Plural}}.Dtos;

            public sealed class Fake{{n.Entity}}ForCreationDto : AutoFaker<{{n.Entity}}ForCreationDto>
            {
                public Fake{{n.Entity}}ForCreationDto({{parameters}})
                {
            {{Block(rules, 8)}}
                }
            }
            """;
    }

    private static IEnumerable<string> InvalidInputs(Spec s)
    {
        var e = s.Names.Entity;
        foreach (var f in s.Fields)
        {
            var set = $"x => x.{f.Name} =";
            switch (f.Kind)
            {
                case Kind.Text:
                    if (!f.Optional)
                        yield return $"yield return () => ({set} \"\", \"{f.Json}\", \"{Missing(s, f)}\");";
                    yield return $"yield return () => ({set} new string('a', {e}.{f.Name}MaxLength + 1), \"{f.Json}\",\n    $\"{TooLong(s, f, e + ".")}\");";
                    yield return $"yield return () => ({set} \"a\\0b\", \"{f.Json}\", \"{BadCharacter(s, f)}\");";
                    break;
                case Kind.Int:
                    yield return $"yield return () => ({set} {e}.{f.Name}Minimum - 1, \"{f.Json}\",\n    $\"{OutOfRange(s, f, e + ".")}\");";
                    yield return $"yield return () => ({set} {e}.{f.Name}Maximum + 1, \"{f.Json}\",\n    $\"{OutOfRange(s, f, e + ".")}\");";
                    break;
                case Kind.Date or Kind.ZonedDate or Kind.Instant:
                    yield return $"yield return () => ({set} default({f.Type}), \"{f.Json}\", \"{Missing(s, f)}\");";
                    break;
            }
        }
    }

    public static string UnitTests(Spec s)
    {
        var n = s.Names;
        var (e, v, t) = (n.Entity, n.Var, s.Title.Name);
        var copy = CopyForUpdate(s, v);
        var step = s.ParentGraphStep;
        var (addFirst, parentAssert) = s.Parent is { } p
            ? ($".Add{p.Entity}(out var {p.Var})", $"\n        {v}.{s.Ref!.Name}.ShouldBe({p.Var}.Id);")
            : ("", "");
        var zoneTest = s.HasZone ? $$"""


            [Test]
            public void a_{{n.Snake}}_refuses_a_time_zone_that_the_server_does_not_know()
            {
                // Arrange
                var graph = TestGraph.Start(out _){{step}};

                // Act
                var act = () => graph.Add{{e}}(out _, x => x.TimeZone = "Mars/Olympus_Mons");

                // Assert
                act.ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<ValidationException>().Property.ShouldBe("timeZone");
            }
            """ : "";
        var usings = Usings([$"{n.Server}.Domain.{n.Plural}", $"{n.Server}.Domain.{n.Plural}.DomainEvents",
            $"{n.Server}.Domain.{n.Plural}.Models", $"{n.Server}.Exceptions", $"{n.App}.SharedTestHelpers.EntityGraphs",
            s.Parent?.BlueprintNamespace, "Shouldly"]);
        return $$"""
            namespace {{n.App}}.UnitTests.Domain.{{n.Plural}};

            {{usings}}

            public class {{e}}Tests
            {
                [Test]
                public void a_new_{{n.Snake}}_is_in_the_tenant_of_its_parent()
                {
                    // Act
                    TestGraph.Start(out var seed){{addFirst}}.Add{{e}}(out var {{v}});

                    // Assert
                    {{v}}.TenantId.ShouldBe(seed.Tenant.Id);{{parentAssert}}
                    {{v}}.DomainEvents.ShouldBe([new {{e}}Created({{v}}.Id)]);
                }

                public static IEnumerable<Func<(Action<{{e}}Input> Customize, string Field, string Message)>> InvalidInputs()
                {
            {{Block(InvalidInputs(s), 8)}}
                }

                [Test]
                [MethodDataSource(nameof(InvalidInputs))]
                public void a_{{n.Snake}}_refuses_an_invalid_value(Action<{{e}}Input> customize, string field, string message)
                {
                    // Arrange
                    var graph = TestGraph.Start(out _){{step}};

                    // Act
                    var act = () => graph.Add{{e}}(out _, customize);

                    // Assert
                    var refusal = act.ShouldThrow<Exception>().GetBaseException().ShouldBeOfType<ValidationException>();
                    refusal.Property.ShouldBe(field);
                    refusal.Message.ShouldBe(message);
                }

                [Test]
                public void an_update_with_another_version_is_refused()
                {
                    // Arrange
                    TestGraph.Start(out _){{step}}.Add{{e}}(out var {{v}});

                    // Act
                    var act = () => {{v}}.Update(ForUpdate({{v}}) with { {{t}} = "Changed", Version = {{v}}.Version + 1 });

                    // Assert
                    act.ShouldThrow<StaleVersionException>();
                    {{v}}.{{t}}.ShouldNotBe("Changed");
                }

                [Test]
                public void an_update_with_the_same_values_changes_nothing()
                {
                    // Arrange
                    TestGraph.Start(out _){{step}}.Add{{e}}(out var {{v}});
                    var events = {{v}}.DomainEvents.Count;

                    // Act
                    {{v}}.Update(ForUpdate({{v}}));

                    // Assert
                    {{v}}.DomainEvents.Count.ShouldBe(events);
                }

                [Test]
                public void an_update_sets_the_values_and_queues_the_update_event()
                {
                    // Arrange
                    TestGraph.Start(out _){{step}}.Add{{e}}(out var {{v}});

                    // Act
                    {{v}}.Update(ForUpdate({{v}}) with { {{t}} = "Changed" });

                    // Assert
                    {{v}}.{{t}}.ShouldBe("Changed");
                    {{v}}.DomainEvents.ShouldContain(new {{e}}Updated({{v}}.Id));
                }

                [Test]
                public void an_update_refuses_an_invalid_value()
                {
                    // Arrange
                    TestGraph.Start(out _){{step}}.Add{{e}}(out var {{v}});

                    // Act
                    var act = () => {{v}}.Update(ForUpdate({{v}}) with { {{t}} = "" });

                    // Assert
                    act.ShouldThrow<ValidationException>().Property.ShouldBe("{{s.Title.Json}}");
                }{{Block([zoneTest], 4)}}

                private static {{e}}ForUpdate ForUpdate({{e}} {{v}}) => new()
                {
            {{Block(copy, 8)}}
                };
            }
            """;
    }

    private static string ForUpdateDtoHelper(Spec s, string indentName = "loaded")
    {
        var copy = CopyForUpdate(s, indentName);
        return $$"""
            private static {{s.Names.Entity}}ForUpdateDto ForUpdate({{s.Names.Entity}}Dto {{indentName}}) => new()
            {
            {{Block(copy, 4)}}
            };
            """;
    }

    public static string CommandTests(Spec s)
    {
        var n = s.Names;
        var (e, v, t) = (n.Entity, n.Var, s.Title.Name);
        var sameValues = s.Fields.Select(f => $"loaded.{f.Name}.ShouldBe(dto.{f.Name});");
        var step = s.ParentGraphStep;
        var (addParent, fake, parentArrange, parentTests) = ("", $"new Fake{e}ForCreationDto()", "", "");
        if (s.Parent is { } p && s.Ref is { } r)
        {
            addParent = $".Add{p.Entity}(out var {p.Var})";
            fake = $"new Fake{e}ForCreationDto({p.Var}.Id)";
            parentArrange = $"\n        await testingServiceScope.StartGraph(out _){addParent}.BuildAsync();";
            parentTests = $$"""


                [Test]
                public async Task an_add_with_a_{{p.Snake}}_of_another_tenant_is_refused()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _).AddTenant(out _){{addParent}}.BuildAsync();

                    // Act
                    var act = () => testingServiceScope.SendAsync(new Add{{e}}.Command({{fake}}.Generate()));

                    // Assert
                    var refusal = await act.ShouldThrowAsync<ValidationException>();
                    refusal.Property.ShouldBe("{{r.Json}}");
                    refusal.Message.ShouldBe("{{EscapeForCs(Texts.ParentMissing(r))}}");
                }

                [Test]
                public async Task an_add_with_a_malformed_{{p.Snake}}_id_is_refused()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();

                    // Act
                    var act = () => testingServiceScope.SendAsync(new Add{{e}}.Command(new Fake{{e}}ForCreationDto("a\0b").Generate()));

                    // Assert
                    (await act.ShouldThrowAsync<ValidationException>()).Property.ShouldBe("{{r.Json}}");
                }
                """;
        }
        var usings = Usings([n.Ns, $"{n.Ns}.Dtos", $"{n.Ns}.Features", $"{n.Server}.Exceptions",
            $"{n.App}.IntegrationTests.Databases", $"{n.App}.SharedTestHelpers.EntityGraphs",
            $"{n.App}.SharedTestHelpers.Fakes.{n.Plural}", $"{n.App}.SharedTestHelpers.Utilities", s.Parent?.BlueprintNamespace,
            "Microsoft.EntityFrameworkCore", "Shouldly"]);
        return $$"""
            namespace {{n.App}}.IntegrationTests.FeatureTests.{{n.Plural}};

            {{usings}}

            public class {{e}}CommandTests : TestBase
            {
                [Test]
                public async Task an_added_{{n.Snake}}_loads_with_its_values()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();{{parentArrange}}
                    var dto = {{fake}}.Generate();

                    // Act
                    var added = await testingServiceScope.SendAsync(new Add{{e}}.Command(dto));

                    // Assert
                    var loaded = await testingServiceScope.SendAsync(new Get{{e}}.Query(added.Id));
            {{Block(sameValues, 8)}}
                }

                [Test]
                public async Task a_member_without_{{n.PermissionName}}_cannot_add_update_or_delete()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out var seed)
                        .AddUser(out var member, u => u.Role = "User")
                        .Under(seed.Tenant){{addParent}}.Add{{e}}(out var {{v}})
                        .BuildAsync();
                    testingServiceScope.SetUserWithDefaults(identifier: member.Identifier);
                    var loaded = await testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));

                    // Act
                    var add = () => testingServiceScope.SendAsync(new Add{{e}}.Command({{fake}}.Generate()));
                    var update = () => testingServiceScope.SendAsync(new Update{{e}}.Command({{v}}.Id, ForUpdate(loaded)));
                    var delete = () => testingServiceScope.SendAsync(new Delete{{e}}.Command({{v}}.Id));

                    // Assert
                    const string refusal = "You cannot {{n.PermissionWords}}. Ask a tenant admin for access.";
                    (await add.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(refusal);
                    (await update.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(refusal);
                    (await delete.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe(refusal);
                }

                [Test]
                public async Task an_update_with_an_old_version_gives_the_stale_version()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _){{step}}.Add{{e}}(out var {{v}}).BuildAsync();
                    var loaded = await testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));
                    await testingServiceScope.SendAsync(new Update{{e}}.Command({{v}}.Id, ForUpdate(loaded) with { {{t}} = "First" }));

                    // Act
                    var act = () => testingServiceScope.SendAsync(
                        new Update{{e}}.Command({{v}}.Id, ForUpdate(loaded) with { {{t}} = "Second" }));

                    // Assert
                    await act.ShouldThrowAsync<StaleVersionException>();
                    (await testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id))).{{t}}.ShouldBe("First");
                }

                [Test]
                public async Task an_update_with_the_same_values_keeps_the_version()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _){{step}}.Add{{e}}(out var {{v}}).BuildAsync();
                    var loaded = await testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));

                    // Act
                    var updated = await testingServiceScope.SendAsync(new Update{{e}}.Command({{v}}.Id, ForUpdate(loaded)));

                    // Assert
                    updated.Version.ShouldBe(loaded.Version);
                }

                [Test]
                public async Task a_deleted_{{n.Snake}}_is_not_found()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _){{step}}.Add{{e}}(out var {{v}}).BuildAsync();

                    // Act
                    await testingServiceScope.SendAsync(new Delete{{e}}.Command({{v}}.Id));

                    // Assert
                    var get = () => testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));
                    await get.ShouldThrowAsync<NotFoundException>();
                    (await IsDeletedAsync({{v}}.Id)).ShouldBeTrue();
                }

                [Test]
                public async Task a_{{n.Snake}}_of_another_tenant_is_not_found()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _).AddTenant(out _){{step}}.Add{{e}}(out var {{v}}).BuildAsync();

                    // Act
                    var get = () => testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));
                    var delete = () => testingServiceScope.SendAsync(new Delete{{e}}.Command({{v}}.Id));

                    // Assert
                    await get.ShouldThrowAsync<NotFoundException>();
                    await delete.ShouldThrowAsync<NotFoundException>();
                    (await IsDeletedAsync({{v}}.Id)).ShouldBeFalse();
                }

                [Test]
                public async Task a_delete_that_a_rival_update_beat_after_the_load_runs_again_and_both_save()
                {
                    // Arrange
                    var pause = PauseAtQuery.After("{{n.SnakePlural}}.delete.load");
                    await using var services = pause.BuildServices();
                    var testingServiceScope = new TestingServiceScope();
                    await testingServiceScope.StartGraph(out _){{step}}.Add{{e}}(out var {{v}}).BuildAsync();
                    var loaded = await testingServiceScope.SendAsync(new Get{{e}}.Query({{v}}.Id));

                    // Act
                    var delete = new TestingServiceScope(services).SendAsync(new Delete{{e}}.Command({{v}}.Id));
                    await pause.ReachedAsync();
                    await new TestingServiceScope().SendAsync(new Update{{e}}.Command({{v}}.Id, ForUpdate(loaded) with { {{t}} = "Rival" }));
                    pause.Release();

                    // Assert
                    await delete;
                    var row = await LoadIgnoringFiltersAsync({{v}}.Id);
                    row.IsDeleted.ShouldBeTrue();
                    row.{{t}}.ShouldBe("Rival");
                }{{Block([parentTests], 4)}}

                private static Task<{{e}}> LoadIgnoringFiltersAsync(string id)
                    => new TestingServiceScope().ExecuteDbContextAsync(db =>
                        db.{{n.Plural}}.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id));

                private static Task<bool> IsDeletedAsync(string id)
                    => new TestingServiceScope().ExecuteDbContextAsync(db =>
                        db.{{n.Plural}}.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => x.IsDeleted).SingleAsync());

            {{Block([ForUpdateDtoHelper(s)], 4)}}
            }
            """;
    }

    public static string ListQueryTests(Spec s)
    {
        var n = s.Names;
        var (e, t) = (n.Entity, s.Title.Name);
        var usings = Usings([$"{n.Ns}.Dtos", $"{n.Ns}.Features", $"{n.Server}.Exceptions",
            $"{n.App}.SharedTestHelpers.EntityGraphs", s.Parent?.BlueprintNamespace, "Shouldly"]);
        return $$"""
            namespace {{n.App}}.IntegrationTests.FeatureTests.{{n.Plural}};

            {{usings}}

            public class {{e}}ListQueryTests : TestBase
            {
                [Test]
                public async Task a_search_finds_the_text_with_no_regard_to_case_and_reads_each_like_character_as_plain_text()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    var marker = Guid.NewGuid().ToString("N");
                    await testingServiceScope.StartGraph(out _){{s.ParentGraphStep}}
                        .Add{{n.Plural}}(out _,
                            x => x.{{t}} = $"North {marker}",
                            x => x.{{t}} = $"{marker} 50% full",
                            x => x.{{t}} = $"{marker} 500 full",
                            x => x.{{t}} = $"{marker} a_b",
                            x => x.{{t}} = $"{marker} axb",
                            x => x.{{t}} = $"{marker} c\\d",
                            x => x.{{t}} = $"{marker} cd")
                        .BuildAsync();

                    // Act
                    var byCase = await SearchAsync(testingServiceScope, $" north {marker.ToUpperInvariant()} ");
                    var percent = await SearchAsync(testingServiceScope, $"{marker} 50%");
                    var underscore = await SearchAsync(testingServiceScope, $"{marker} a_b");
                    var backslash = await SearchAsync(testingServiceScope, $"{marker} c\\d");

                    // Assert
                    byCase.ShouldBe([$"North {marker}"]);
                    percent.ShouldBe([$"{marker} 50% full"]);
                    underscore.ShouldBe([$"{marker} a_b"]);
                    backslash.ShouldBe([$"{marker} c\\d"]);
                }

                [Test]
                public async Task a_search_longer_than_its_maximum_gives_a_query_input_error()
                {
                    // Arrange
                    var testingServiceScope = new TestingServiceScope();
                    var search = new string('a', {{e}}ParametersDto.SearchMaxLength + 1);

                    // Act
                    var act = () => SearchAsync(testingServiceScope, search);

                    // Assert
                    (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe("search");
                }

                private static async Task<List<string>> SearchAsync(TestingServiceScope scope, string search)
                {
                    var page = await scope.SendAsync(new Get{{e}}List.Query({{e}}ParametersDto.From(null, null, null, null, search)));
                    return page.Select(x => x.{{t}}).ToList();
                }
            }
            """;
    }

    public static string TenantDeleteRaceTests(Spec s)
    {
        var n = s.Names;
        var e = n.Entity;
        var (parent, fake) = s.Parent is { } p
            ? ($"\n            .Under(tenant).Add{p.Entity}(out var {p.Var})", $"new Fake{e}ForCreationDto({p.Var}.Id)")
            : ("", $"new Fake{e}ForCreationDto()");
        var usings = Usings([$"{n.Ns}.Features", $"{n.Server}.Domain.Tenants.Features", $"{n.Server}.Exceptions",
            $"{n.Server}.Resources", $"{n.App}.IntegrationTests.Databases", $"{n.App}.SharedTestHelpers.EntityGraphs",
            $"{n.App}.SharedTestHelpers.Fakes.{n.Plural}", $"{n.App}.SharedTestHelpers.Utilities", s.Parent?.BlueprintNamespace,
            "Microsoft.EntityFrameworkCore", "Shouldly"]);
        return $$"""
            namespace {{n.App}}.IntegrationTests.FeatureTests.{{n.Plural}};

            {{usings}}

            /// <summary>
            /// A tenant delete deletes the {{n.WordsPlural}} of the tenant. An add at the same time must not leave a live
            /// {{n.Words}} in the deleted tenant, so each takes the lock of the tenant, and the second waits for the first.
            /// See <c>docs/backend/concurrency.md</c>.
            /// </summary>
            public class {{e}}TenantDeleteRaceTests : TestBase
            {
                [Test]
                public async Task an_add_during_a_tenant_delete_waits_and_then_finds_no_tenant()
                {
                    // Arrange
                    var pause = PauseAtQuery.After("tenants.delete.load_{{n.SnakePlural}}");
                    await using var services = pause.BuildServices();
                    await new TestingServiceScope().StartGraph(out _)
                        .AddTenant(out var tenant)
                        .AddUser(out var admin, u => u.Role = "Admin"){{parent}}
                        .BuildAsync();
                    var underTest = new TestingServiceScope(services);
                    var rival = new TestingServiceScope();
                    underTest.SetUserWithDefaults(identifier: admin.Identifier);
                    rival.SetUserWithDefaults(identifier: admin.Identifier);

                    // Act
                    var delete = underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
                    await pause.ReachedAsync();
                    var add = rival.SendAsync(new Add{{e}}.Command({{fake}}.Generate()));
                    await pause.WaitUntilBlockedAsync(add);
                    pause.Release();

                    // Assert
                    await delete;
                    (await Should.ThrowAsync<NotFoundException>(add)).Message.ShouldBe("The tenant does not exist.");
                    (await LiveCountAsync(tenant.Id)).ShouldBe(0);
                }

                [Test]
                public async Task a_tenant_delete_during_an_add_waits_and_then_deletes_the_new_{{n.Snake}}()
                {
                    // Arrange
                    var pause = PauseAtQuery.After("{{n.SnakePlural}}.add.tenant_exists");
                    await using var services = pause.BuildServices();
                    await new TestingServiceScope().StartGraph(out _)
                        .AddTenant(out var tenant)
                        .AddUser(out var admin, u => u.Role = "Admin"){{parent}}
                        .BuildAsync();
                    var underTest = new TestingServiceScope(services);
                    var rival = new TestingServiceScope();
                    underTest.SetUserWithDefaults(identifier: admin.Identifier);
                    rival.SetUserWithDefaults(identifier: admin.Identifier);

                    // Act
                    var add = underTest.SendAsync(new Add{{e}}.Command({{fake}}.Generate()));
                    await pause.ReachedAsync();
                    var delete = rival.SendAsync(new DeleteTenant.Command(tenant.Id));
                    await pause.WaitUntilBlockedAsync(delete);
                    pause.Release();

                    // Assert
                    await add;
                    await delete;
                    (await LiveCountAsync(tenant.Id)).ShouldBe(0);
                }

                private static Task<int> LiveCountAsync(string tenantId)
                    => new TestingServiceScope().ExecuteDbContextAsync(db =>
                        db.{{n.Plural}}.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(x => x.TenantId == tenantId));
            }
            """;
    }

    private static string ParentGraphUsing(Spec s)
        => s.Parent is { } p && p.BlueprintNamespace != $"{s.Names.App}.SharedTestHelpers.EntityGraphs" ? $"\nusing {p.BlueprintNamespace};" : "";

    private static string FunctionalArrange(Spec s) => $$"""
        private async Task<{{s.Names.Entity}}> Add{{s.Names.Entity}}AndSignInAsync(string role)
        {
            await StartGraph()
                .AddTenant(out var tenant)
                .AddUser(out var user, u => u.Role = role)
                .Under(tenant){{s.ParentGraphStep}}.Add{{s.Names.Entity}}(out var {{s.Names.Var}})
                .BuildAsync();
            SignInAs(user.Identifier);
            return {{s.Names.Var}};
        }
        """;

    public static string HttpTests(Spec s)
    {
        var n = s.Names;
        var (e, v, t) = (n.Entity, n.Var, s.Title.Name);
        var (keep, fake) = s.Ref is { } r ? ($"var {v} = ", $"new Fake{e}ForCreationDto({v}.{r.Name})") : ("", $"new Fake{e}ForCreationDto()");
        return $$"""
            namespace {{n.App}}.FunctionalTests.FunctionalTests.{{n.Plural}};

            using System.Net;
            using System.Net.Http.Json;
            using {{n.Server}}.Domain.{{n.Plural}};
            using {{n.Server}}.Domain.{{n.Plural}}.Dtos;
            using {{n.Server}}.Resources.Entrypoints;
            using {{n.App}}.SharedTestHelpers.EntityGraphs;{{ParentGraphUsing(s)}}
            using {{n.App}}.SharedTestHelpers.Fakes.{{n.Plural}};
            using Microsoft.AspNetCore.Mvc;
            using Shouldly;
            using TestUtilities;

            public class {{e}}HttpTests : TestBase
            {
                private const string Route = $"{ApiRoutes.Base}/{{n.KebabPlural}}";

                [Test]
                public async Task an_admin_adds_updates_and_deletes_{{n.A}}_{{n.Snake}}()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("Admin");
                    var added = await FactoryClient.PostJsonRequestAsync(Route, {{fake}}.Generate());
                    added.StatusCode.ShouldBe(HttpStatusCode.Created);
                    var created = (await added.Content.ReadFromJsonAsync<{{e}}Dto>()).ShouldNotBeNull();
                    added.Headers.Location.ShouldNotBeNull().ToString().ShouldEndWith($"/{{n.KebabPlural}}/{created.Id}");

                    // Act
                    var updated = await FactoryClient.PutJsonRequestAsync($"{Route}/{created.Id}", ForUpdate(created) with { {{t}} = "Changed" });
                    var deleted = await FactoryClient.DeleteRequestAsync($"{Route}/{created.Id}");
                    var read = await FactoryClient.GetRequestAsync($"{Route}/{created.Id}");

                    // Assert
                    updated.StatusCode.ShouldBe(HttpStatusCode.OK);
                    (await updated.Content.ReadFromJsonAsync<{{e}}Dto>()).ShouldNotBeNull().{{t}}.ShouldBe("Changed");
                    deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
                    read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                }

                [Test]
                public async Task a_member_reads_and_lists_but_cannot_add()
                {
                    // Arrange
                    var {{v}} = await Add{{e}}AndSignInAsync("User");

                    // Act
                    var read = await FactoryClient.GetRequestAsync($"{Route}/{{{v}}.Id}");
                    var list = await FactoryClient.GetRequestAsync(Route);
                    var add = await FactoryClient.PostJsonRequestAsync(Route, {{fake}}.Generate());

                    // Assert
                    read.StatusCode.ShouldBe(HttpStatusCode.OK);
                    list.StatusCode.ShouldBe(HttpStatusCode.OK);
                    (await list.Content.ReadFromJsonAsync<List<{{e}}Dto>>()).ShouldNotBeNull().ShouldHaveSingleItem().Id.ShouldBe({{v}}.Id);
                    add.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
                }

                [Test]
                public async Task an_update_with_an_old_version_gives_409()
                {
                    // Arrange
                    var {{v}} = await Add{{e}}AndSignInAsync("Admin");
                    var loaded = (await (await FactoryClient.GetRequestAsync($"{Route}/{{{v}}.Id}"))
                        .Content.ReadFromJsonAsync<{{e}}Dto>()).ShouldNotBeNull();
                    var first = await FactoryClient.PutJsonRequestAsync($"{Route}/{{{v}}.Id}", ForUpdate(loaded) with { {{t}} = "First" });
                    first.StatusCode.ShouldBe(HttpStatusCode.OK);

                    // Act
                    var second = await FactoryClient.PutJsonRequestAsync($"{Route}/{{{v}}.Id}", ForUpdate(loaded) with { {{t}} = "Second" });

                    // Assert
                    second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
                    (await second.Content.ReadFromJsonAsync<ProblemDetails>()).ShouldNotBeNull().Type.ShouldBe(DomainProblemDetails.StaleVersionType);
                }

            {{Block([FunctionalArrange(s)], 4)}}

                private void SignInAs(string identifier) => FactoryClient.SignInAsAppUser(identifier);

            {{Block([ForUpdateDtoHelper(s)], 4)}}
            }
            """;
    }

    private static string SampleValue(Spec s, Field f, bool forMcp, string entity) => f.Kind switch
    {
        Kind.Text => $"\"Sample {f.Words}\"",
        Kind.Int => $"{entity}.{f.Name}Minimum",
        Kind.Bool => "true",
        Kind.Date or Kind.ZonedDate => forMcp ? "\"2026-01-02\"" : "new DateOnly(2026, 1, 2)",
        Kind.TimeZone => "\"America/Chicago\"",
        Kind.ParentId => f.Camel,
        Kind.Instant => forMcp ? "\"2027-01-15T08:00:00Z\"" : "Timestamp.FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(1_800_000_000))",
    };

    public static string McpTests(Spec s)
    {
        var n = s.Names;
        var (e, v, t) = (n.Entity, n.Var, s.Title.Name);
        var arguments = s.Fields.Select(f => $"[\"{f.Json}\"] = {SampleValue(s, f, forMcp: true, e)},");
        var (keep, parentId, parameter) = s.Ref is { } r ? ($"var {v} = ", $"{v}.{r.Name}", $"string {r.Camel}") : ("", "", "");
        var removes = s.Fields.Where(f => f.CreateOnly).Select(f => $"\nupdate.Remove(\"{f.Json}\");");
        return $$"""
            namespace {{n.App}}.FunctionalTests.FunctionalTests.{{n.Plural}};

            using {{n.Server}}.Domain.{{n.Plural}};
            using {{n.App}}.SharedTestHelpers.EntityGraphs;{{ParentGraphUsing(s)}}
            using ModelContextProtocol.Client;
            using Shouldly;
            using TestUtilities;

            public class {{e}}McpToolTests : TestBase
            {
                private static readonly string[] Tools =
                    ["add_{{n.Snake}}", "get_{{n.Snake}}", "list_{{n.SnakePlural}}", "update_{{n.Snake}}"];

                [Test]
                [Arguments("User", new[] { "get_{{n.Snake}}", "list_{{n.SnakePlural}}" })]
                [Arguments("Admin", new[] { "add_{{n.Snake}}", "get_{{n.Snake}}", "list_{{n.SnakePlural}}", "update_{{n.Snake}}" })]
                public async Task tools_list_shows_only_the_tools_that_the_caller_can_use(string role, string[] expected)
                {
                    // Arrange
                    await Add{{e}}AndSignInAsync(role);
                    await using var client = await CreateMcpClientAsync();

                    // Act
                    var tools = await client.ListToolsAsync();

                    // Assert
                    tools.Select(t => t.Name).Where(Tools.Contains).ShouldBe(expected, ignoreOrder: true);
                }

                [Test]
                public async Task a_member_reads_and_lists_with_the_tools()
                {
                    // Arrange
                    var {{v}} = await Add{{e}}AndSignInAsync("User");
                    await using var client = await CreateMcpClientAsync();

                    // Act
                    var read = await client.CallToolAsync("get_{{n.Snake}}", new Dictionary<string, object?> { ["id"] = {{v}}.Id });
                    var list = await client.CallToolAsync("list_{{n.SnakePlural}}", new Dictionary<string, object?>());

                    // Assert
                    read.IsError.ShouldNotBe(true);
                    read.StructuredContent.ShouldNotBeNull().GetProperty("{{s.Title.Json}}").GetString().ShouldBe({{v}}.{{t}});
                    list.IsError.ShouldNotBe(true);
                    list.StructuredContent.ShouldNotBeNull().GetProperty("items").EnumerateArray().ShouldHaveSingleItem()
                        .GetProperty("id").GetString().ShouldBe({{v}}.Id);
                }

                [Test]
                public async Task a_member_cannot_add_with_the_tool()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("User");
                    await using var client = await CreateMcpClientAsync();

                    // Act
                    var result = await client.CallToolAsync("add_{{n.Snake}}", AddArguments({{parentId}}));

                    // Assert
                    result.ShouldBeToolError("permission_denied");
                }

                [Test]
                public async Task an_admin_adds_and_updates_with_the_tools()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("Admin");
                    await using var client = await CreateMcpClientAsync();
                    var added = (await client.CallToolAsync("add_{{n.Snake}}", AddArguments({{parentId}}))).StructuredContent.ShouldNotBeNull();
                    var update = AddArguments({{parentId}});{{Block(removes, 8)}}
                    update["id"] = added.GetProperty("id").GetString();
                    update["{{s.Title.Json}}"] = "Changed";
                    update["version"] = added.GetProperty("version").GetUInt32();

                    // Act
                    var result = await client.CallToolAsync("update_{{n.Snake}}", update);

                    // Assert
                    result.IsError.ShouldNotBe(true);
                    result.StructuredContent.ShouldNotBeNull().GetProperty("{{s.Title.Json}}").GetString().ShouldBe("Changed");
                }

                private static Dictionary<string, object?> AddArguments({{parameter}}) => new()
                {
            {{Block(arguments, 8)}}
                };

            {{Block([FunctionalArrange(s)], 4)}}

                private void SignInAs(string identifier) => FactoryClient.SignInAsAppUser(identifier);
            }
            """;
    }

    public static string GrpcTests(Spec s)
    {
        var n = s.Names;
        var (e, v, t) = (n.Entity, n.Var, s.Title.Name);
        var domain = $"Domain{e}";
        var values = s.Fields.Select(f => $"{f.Name} = {SampleValue(s, f, forMcp: false, domain)},");
        var updated = s.Updatable.ToList();
        var (returns, keep, parentId, parameter, addParent, returnParent) = s.Parent is { } p && s.Ref is { } r
            ? ("Task<string>", "var parentId = ", "parentId", $"string {r.Camel}", $"\n            .Under(tenant).Add{p.Entity}(out var {p.Var})",
                $"\n        return {p.Var}.Id;")
            : ("Task", "", "", "", "", "");
        var usings = new List<string>
        {
            $"{n.Server}.Domain.{n.Plural}.Grpc.V1",
            $"{n.App}.SharedTestHelpers.EntityGraphs",
        };
        if (s.Parent is { } graph && graph.BlueprintNamespace != usings[^1])
            usings.Add(graph.BlueprintNamespace);
        if (HasInstant(s))
            usings.Add("global::Google.Protobuf.WellKnownTypes");
        usings.AddRange(["global::Grpc.Core", "Shouldly", "TestUtilities"]);
        var usesDomain = s.Fields.Any(f => f.Kind == Kind.Int);
        return $$"""
            namespace {{n.App}}.FunctionalTests.FunctionalTests.{{n.Plural}};

            {{string.Join("\n", usings.Select(u => $"using {u};"))}}{{(usesDomain ? $"\nusing {domain} = {n.Ns}.{e};" : "")}}

            public class {{e}}GrpcTests : TestBase
            {
                [Test]
                public async Task an_admin_adds_reads_lists_updates_and_deletes_with_grpc()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("Admin");
                    using var channel = CreateGrpcChannel();
                    var client = new {{n.GrpcService}}.{{n.GrpcService}}Client(channel);
                    var added = (await client.Add{{e}}Async(AddRequest({{parentId}}))).{{e}};

                    // Act
                    var read = await client.Get{{e}}Async(new Get{{e}}Request { Id = added.Id });
                    var list = await client.List{{n.Plural}}Async(new List{{n.Plural}}Request { Search = added.{{t}} });
                    var updated = await client.Update{{e}}Async(UpdateRequest(added, "Changed"));
                    await client.Delete{{e}}Async(new Delete{{e}}Request { Id = added.Id });

                    // Assert
                    read.{{e}}.ShouldBe(added);
                    list.{{n.Plural}}.ShouldContain(added);
                    updated.{{e}}.{{t}}.ShouldBe("Changed");
                    var exception = await Should.ThrowAsync<RpcException>(
                        async () => await client.Get{{e}}Async(new Get{{e}}Request { Id = added.Id }));
                    exception.StatusCode.ShouldBe(StatusCode.NotFound);
                }

                [Test]
                public async Task a_member_cannot_add_with_grpc()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("User");
                    using var channel = CreateGrpcChannel();
                    var client = new {{n.GrpcService}}.{{n.GrpcService}}Client(channel);

                    // Act
                    var exception = await Should.ThrowAsync<RpcException>(async () => await client.Add{{e}}Async(AddRequest({{parentId}})));

                    // Assert
                    exception.StatusCode.ShouldBe(StatusCode.PermissionDenied);
                }

                [Test]
                public async Task an_update_with_an_old_version_is_aborted()
                {
                    // Arrange
                    {{keep}}await Add{{e}}AndSignInAsync("Admin");
                    using var channel = CreateGrpcChannel();
                    var client = new {{n.GrpcService}}.{{n.GrpcService}}Client(channel);
                    var added = (await client.Add{{e}}Async(AddRequest({{parentId}}))).{{e}};
                    await client.Update{{e}}Async(UpdateRequest(added, "First"));

                    // Act
                    var exception = await Should.ThrowAsync<RpcException>(
                        async () => await client.Update{{e}}Async(UpdateRequest(added, "Second")));

                    // Assert
                    exception.StatusCode.ShouldBe(StatusCode.Aborted);
                }

                private static Add{{e}}Request AddRequest({{parameter}}) => new()
                {
            {{Block(values, 8)}}
                };

                private static Update{{e}}Request UpdateRequest({{e}} {{v}}, string {{s.Title.Camel}})
                {
                    var request = new Update{{e}}Request
                    {
                        Id = {{v}}.Id,
            {{Block(updated.Where(f => !f.Optional).Select(f => $"{f.Name} = {v}.{f.Name},"), 12)}}
                        Version = {{v}}.Version,
                    };
            {{Block(updated.Where(f => f.Optional).Select(f => $"if ({v}.Has{f.Name})\n    request.{f.Name} = {v}.{f.Name};"), 8)}}
                    request.{{t}} = {{s.Title.Camel}};
                    return request;
                }

                private async {{returns}} Add{{e}}AndSignInAsync(string role)
                {
                    await StartGraph()
                        .AddTenant(out {{(s.Parent is null ? "_" : "var tenant")}})
                        .AddUser(out var user, u => u.Role = role){{addParent}}
                        .BuildAsync();
                    FactoryClient.SignInAsAppUser(user.Identifier);{{returnParent}}
                }
            }
            """;
    }
}
