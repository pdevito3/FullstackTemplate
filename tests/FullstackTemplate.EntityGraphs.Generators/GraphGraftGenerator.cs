#nullable enable

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace FullstackTemplate.EntityGraphs.Generators;

[Generator]
public sealed class GraphGraftGenerator : IIncrementalGenerator
{
    private const string GraphGraftAttribute = "FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.GraphGraftAttribute";
    private const string GraftAttribute = "FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.GraftAttribute";
    private const string NoGraftAttribute = "FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.NoGraftAttribute";
    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    private static readonly DiagnosticDescriptor DuplicateMethod = new(
        "GRAPH001",
        "EntityGraph graft method collision",
        "Blueprint members '{0}' and '{1}' produce overlapping graft overloads on the same builder: {2}",
        "EntityGraph graft",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Rename one graft with [Graft(\"Name\")] so callers can identify the intended blueprint."
    );

    private static readonly DiagnosticDescriptor SameSingularAndPlural = new(
        "GRAPH002",
        "EntityGraph graft plural matches singular",
        "Blueprint member '{0}' uses '{1}' for both singular and plural; set [Graft(Plural = \"...\")]",
        "EntityGraph graft",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Choose a distinct plural graft suffix with [Graft(Plural = \"...\")]."
    );

    private static readonly DiagnosticDescriptor InvalidCatalog = new(
        "GRAPH003",
        "Invalid EntityGraph graft catalog",
        "[GraphGraft] supports only top-level, non-generic, non-file-local static classes",
        "EntityGraph graft",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Move the catalog to a top-level static class without type parameters or file-local scope."
    );

    private static readonly DiagnosticDescriptor InvalidGraftName = new(
        "GRAPH004",
        "Invalid EntityGraph graft name",
        "Blueprint member '{0}' has an invalid {1} graft name '{2}'; use a valid C# identifier",
        "EntityGraph graft",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Use a valid C# identifier for the singular or plural graft name."
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var catalogs = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                GraphGraftAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => CreateCatalogModel(attributeContext)
            )
            .WithTrackingName("GraphGraftCatalogModel");

        var generatedSources = catalogs
            .Where(static catalog => catalog.IsSupported)
            .Select(
                static (catalog, _) =>
                    new GeneratedSourceModel(CreateHintName(catalog), RenderCatalog(catalog))
            )
            .WithTrackingName("GraphGraftSource");
        var catalogsWithCompilation = catalogs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(
            catalogsWithCompilation,
            static (productionContext, pair) =>
                ReportCatalogDiagnostics(productionContext, pair.Left, pair.Right)
        );
        context.RegisterSourceOutput(
            generatedSources,
            static (productionContext, generated) =>
                productionContext.AddSource(
                    generated.HintName,
                    SourceText.From(generated.Source, Encoding.UTF8)
                )
        );
    }

    private static CatalogModel CreateCatalogModel(GeneratorAttributeSyntaxContext context)
    {
        var catalog = (INamedTypeSymbol)context.TargetSymbol;
        var isFileLocal =
            context.TargetNode is TypeDeclarationSyntax declaration
            && declaration.Modifiers.Any(SyntaxKind.FileKeyword);
        var isSupported =
            catalog.IsStatic
            && catalog.ContainingType is null
            && !catalog.IsGenericType
            && !isFileLocal;

        var members = ImmutableArray.CreateBuilder<BlueprintMemberModel>();
        if (isSupported)
        {
            foreach (var member in catalog.GetMembers())
            {
                if (
                    HasAttribute(member, NoGraftAttribute)
                    || !IsAccessibleFromGeneratedSibling(member)
                )
                {
                    continue;
                }

                ITypeSymbol? memberType = member switch
                {
                    IFieldSymbol field
                        when field.IsStatic
                            && field.IsReadOnly
                            && !field.IsConst
                            && !field.IsImplicitlyDeclared => field.Type,
                    IPropertySymbol property
                        when property.GetMethod?.IsStatic == true && property.SetMethod is null =>
                        property.Type,
                    _ => null,
                };

                if (
                    memberType is not INamedTypeSymbol namedType
                    || !TryGetBlueprintKind(
                        namedType,
                        out var kind,
                        out var seedType,
                        out var parentType,
                        out var inputType,
                        out var outputType
                    )
                )
                {
                    continue;
                }

                var (nameOverride, pluralOverride) = ReadGraftNames(member);
                var name = nameOverride ?? member.Name;
                var plural = pluralOverride ?? Pluralize(name);
                var isValidName = IsValidGraftName(name);
                var isValidPlural = pluralOverride is null
                    ? isValidName
                    : IsValidGraftPlural(plural);
                members.Add(
                    new BlueprintMemberModel(
                        member.Name,
                        name,
                        plural ?? Pluralize(name),
                        EscapeIdentifier(member.Name),
                        seedType,
                        parentType,
                        inputType,
                        outputType,
                        kind,
                        GetAccessibility(member.DeclaredAccessibility),
                        isValidName,
                        isValidPlural,
                        pluralOverride is not null,
                        SourceLocationModel.From(
                            GetMemberLocation(member, context.TargetNode.GetLocation()),
                            context.SemanticModel.Compilation
                        )
                    )
                );
            }
        }

        var namespaceName = catalog.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : catalog.ContainingNamespace.ToDisplayString();

        return new CatalogModel(
            catalog.Name,
            catalog.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            namespaceName,
            GetAccessibility(catalog.DeclaredAccessibility),
            isSupported,
            SourceLocationModel.From(
                context.TargetNode.GetLocation(),
                context.SemanticModel.Compilation
            ),
            members.ToImmutable()
        );
    }

    private static void ReportCatalogDiagnostics(
        SourceProductionContext context,
        CatalogModel catalog,
        Compilation compilation
    )
    {
        if (!catalog.IsSupported)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(InvalidCatalog, catalog.Location.ToLocation(compilation))
            );
            return;
        }

        ReportMemberDiagnostics(context, catalog, compilation);
    }

    private static string CreateHintName(CatalogModel catalog) =>
        catalog.Namespace.Length == 0
            ? $"{catalog.Name}.Grafts.g.cs"
            : $"{catalog.Namespace.Replace('.', '/')}/{catalog.Name}.Grafts.g.cs";

    private static void ReportMemberDiagnostics(
        SourceProductionContext context,
        CatalogModel catalog,
        Compilation compilation
    )
    {
        foreach (var member in catalog.Members)
        {
            if (!member.IsValidName)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidGraftName,
                        member.Location.ToLocation(compilation),
                        member.SourceName,
                        "singular",
                        member.Name
                    )
                );
            }

            if (!member.IsValidPlural && (member.IsValidName || member.HasPluralOverride))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        InvalidGraftName,
                        member.Location.ToLocation(compilation),
                        member.SourceName,
                        "plural",
                        member.Plural
                    )
                );
            }

            if (
                member.IsValid
                && string.Equals(member.Name, member.Plural, StringComparison.Ordinal)
            )
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        SameSingularAndPlural,
                        member.Location.ToLocation(compilation),
                        member.SourceName,
                        member.Name
                    )
                );
            }
        }

        var members = catalog.Members.Where(member => member.IsValid).ToArray();
        for (var i = 0; i < members.Length; i++)
        {
            for (var j = i + 1; j < members.Length; j++)
            {
                var left = members[i];
                var right = members[j];
                if (!ReceiversOverlap(left, right))
                {
                    continue;
                }

                var leftMethods = CreateMethodModels(left).ToArray();
                var rightMethods = CreateMethodModels(right).ToArray();
                var overloads = leftMethods
                    .Where(leftMethod =>
                        rightMethods.Any(rightMethod =>
                            leftMethod.Name == rightMethod.Name
                            && leftMethod.Parameters.SequenceEqual(
                                rightMethod.Parameters,
                                StringComparer.Ordinal
                            )
                        )
                    )
                    .Select(method => method.DisplaySignature)
                    .ToArray();
                if (overloads.Length > 0)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            DuplicateMethod,
                            left.Location.ToLocation(compilation),
                            new[] { right.Location.ToLocation(compilation) },
                            properties: null,
                            left.SourceName,
                            right.SourceName,
                            string.Join("; ", overloads)
                        )
                    );
                }
            }
        }
    }

    private static IEnumerable<MethodModel> CreateMethodModels(BlueprintMemberModel member)
    {
        var customizer = member.InputType ?? member.OutputType;
        yield return new MethodModel(
            $"Add{member.Name}",
            new[] { $"out {member.OutputType}", $"global::System.Action<{customizer}>?" }
        );
        yield return new MethodModel(
            $"Add{member.Plural}",
            new[] { $"out {member.OutputType}[]", "int" }
        );
        yield return new MethodModel(
            $"Add{member.Plural}",
            new[] { $"out {member.OutputType}[]", $"params global::System.Action<{customizer}>[]" }
        );
    }

    private static bool ReceiversOverlap(BlueprintMemberModel left, BlueprintMemberModel right)
    {
        if (left.SeedType != right.SeedType)
        {
            return false;
        }

        if (IsRootKind(left.Kind) || IsRootKind(right.Kind))
        {
            return true;
        }

        return left.ParentType == right.ParentType;
    }

    private static string RenderCatalog(CatalogModel catalog)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine("using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;");
        source.AppendLine();
        if (catalog.Namespace.Length > 0)
        {
            source.Append("namespace ").Append(catalog.Namespace).AppendLine(";");
            source.AppendLine();
        }

        source.AppendLine(
            "/// <summary>Generated Add graft methods for a blueprint catalog.</summary>"
        );
        source
            .Append(catalog.Accessibility)
            .Append(" static class ")
            .Append(catalog.Name)
            .AppendLine("Grafts");
        source.AppendLine("{");

        foreach (var member in catalog.Members)
        {
            if (member.IsValid)
            {
                RenderMethods(source, catalog, member);
            }
        }

        source.AppendLine("}");
        return source.ToString();
    }

    private static void RenderMethods(
        StringBuilder source,
        CatalogModel catalog,
        BlueprintMemberModel member
    )
    {
        var customizeType = member.InputType ?? member.OutputType;
        var singularParameterNames = AllocateUniqueParameterNames(
            CamelCase(member.Name),
            "b",
            "customize"
        );
        var pluralCountParameterNames = AllocateUniqueParameterNames(
            CamelCase(member.Plural),
            "b",
            "count"
        );
        var pluralCustomizeParameterNames = AllocateUniqueParameterNames(
            CamelCase(member.Plural),
            "b",
            "customize"
        );
        var singularOutputName = EscapeIdentifier(singularParameterNames[0]);
        var singularBuilderName = EscapeIdentifier(singularParameterNames[1]);
        var singularCustomizeName = EscapeIdentifier(singularParameterNames[2]);
        var pluralOutputName = EscapeIdentifier(pluralCountParameterNames[0]);
        var pluralCountBuilderName = EscapeIdentifier(pluralCountParameterNames[1]);
        var countName = EscapeIdentifier(pluralCountParameterNames[2]);
        var pluralCustomizeOutputName = EscapeIdentifier(pluralCustomizeParameterNames[0]);
        var pluralCustomizeBuilderName = EscapeIdentifier(pluralCustomizeParameterNames[1]);
        var pluralCustomizeName = EscapeIdentifier(pluralCustomizeParameterNames[2]);
        var extensionReceiver = IsRootKind(member.Kind)
            ? $"global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.GraphBuilder<{member.SeedType}, TAnchor>"
            : $"global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.GraphBuilder<{member.SeedType}, {member.ParentType}[]>";
        var returnType =
            $"global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.GraphBuilder<{member.SeedType}, {member.OutputType}[]>";
        var rootGeneric = IsRootKind(member.Kind) ? "<TAnchor>" : string.Empty;

        source.AppendLine("    /// <summary>Creates one default blueprint output.</summary>");
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(' ')
            .Append("Add")
            .Append(member.Name)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(' ')
            .Append(singularBuilderName)
            .Append(", out ")
            .Append(member.OutputType)
            .Append(' ')
            .Append(singularOutputName)
            .Append(", global::System.Action<")
            .Append(customizeType)
            .Append(">? ")
            .Append(singularCustomizeName)
            .Append(" = null) =>\n")
            .Append("        ")
            .Append(singularBuilderName)
            .Append(".AddOne(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(singularOutputName)
            .Append(", ")
            .Append(singularCustomizeName)
            .AppendLine(");");

        source.AppendLine(
            "    /// <summary>Creates the requested number of blueprint outputs.</summary>"
        );
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(" Add")
            .Append(member.Plural)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(' ')
            .Append(pluralCountBuilderName)
            .Append(", out ")
            .Append(member.OutputType)
            .Append("[] ")
            .Append(pluralOutputName)
            .Append(", int ")
            .Append(countName)
            .AppendLine(") =>")
            .Append("        ")
            .Append(pluralCountBuilderName)
            .Append(".Add(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(pluralOutputName)
            .Append(", ")
            .Append(countName)
            .AppendLine(");");

        source.AppendLine("    /// <summary>Creates one output per customizer.</summary>");
        source
            .Append("    ")
            .Append(member.Accessibility)
            .Append(" static ")
            .Append(returnType)
            .Append(" Add")
            .Append(member.Plural)
            .Append(rootGeneric)
            .Append("(this ")
            .Append(extensionReceiver)
            .Append(' ')
            .Append(pluralCustomizeBuilderName)
            .Append(", out ")
            .Append(member.OutputType)
            .Append("[] ")
            .Append(pluralCustomizeOutputName)
            .Append(", params global::System.Action<")
            .Append(customizeType)
            .Append(">[] ")
            .Append(pluralCustomizeName)
            .Append(") =>\n")
            .Append("        ")
            .Append(pluralCustomizeBuilderName)
            .Append(".Add(")
            .Append(catalog.QualifiedName)
            .Append('.')
            .Append(member.EscapedSourceName)
            .Append(", out ")
            .Append(pluralCustomizeOutputName)
            .Append(", ")
            .Append(pluralCustomizeName)
            .AppendLine(");")
            .AppendLine();
    }

    private static bool TryGetBlueprintKind(
        INamedTypeSymbol type,
        out BlueprintKind kind,
        out string seedType,
        out string? parentType,
        out string? inputType,
        out string outputType
    )
    {
        var definition = type.OriginalDefinition.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat
        );
        var arguments = type.TypeArguments;
        parentType = null;
        inputType = null;
        switch (definition)
        {
            case "global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.Blueprint<TSeed, T>":
                kind = BlueprintKind.Root;
                seedType = Format(arguments[0]);
                outputType = Format(arguments[1]);
                return true;
            case "global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.InputBlueprint<TSeed, TInput, T>":
                kind = BlueprintKind.RootInput;
                seedType = Format(arguments[0]);
                inputType = Format(arguments[1]);
                outputType = Format(arguments[2]);
                return true;
            case "global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.ChildBlueprint<TSeed, TParent, T>":
                kind = BlueprintKind.Child;
                seedType = Format(arguments[0]);
                parentType = Format(arguments[1]);
                outputType = Format(arguments[2]);
                return true;
            case "global::FullstackTemplate.SharedTestHelpers.EntityGraphs.Core.ChildInputBlueprint<TSeed, TParent, TInput, T>":
                kind = BlueprintKind.ChildInput;
                seedType = Format(arguments[0]);
                parentType = Format(arguments[1]);
                inputType = Format(arguments[2]);
                outputType = Format(arguments[3]);
                return true;
            default:
                kind = default;
                seedType = string.Empty;
                outputType = string.Empty;
                return false;
        }
    }

    private static string Format(ITypeSymbol type) =>
        type.ToDisplayString(FullyQualifiedNullableFormat);

    private static bool IsRootKind(BlueprintKind kind) =>
        kind is BlueprintKind.Root or BlueprintKind.RootInput;

    private static bool HasAttribute(ISymbol symbol, string metadataName) =>
        symbol
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);

    private static (string? Name, string? Plural) ReadGraftNames(ISymbol member)
    {
        var attribute = member
            .GetAttributes()
            .FirstOrDefault(item => item.AttributeClass?.ToDisplayString() == GraftAttribute);
        if (attribute is null)
        {
            return (null, null);
        }

        var name =
            attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value as string
                : null;
        var plural =
            attribute
                .NamedArguments.FirstOrDefault(argument => argument.Key == "Plural")
                .Value.Value as string;
        return (name, plural);
    }

    private static bool IsAccessibleFromGeneratedSibling(ISymbol member) =>
        member.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Internal
                or Accessibility.ProtectedOrInternal;

    private static bool IsValidGraftName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && SyntaxFacts.IsValidIdentifier($"Add{name}")
        && IsValidOutputParameterName(name);

    private static bool IsValidGraftPlural(string plural) =>
        !string.IsNullOrWhiteSpace(plural)
        && SyntaxFacts.IsValidIdentifier($"Add{plural}")
        && IsValidOutputParameterName(plural);

    private static bool IsValidOutputParameterName(string name)
    {
        var parameterName = CamelCase(name);
        return SyntaxFacts.IsValidIdentifier(parameterName)
            || SyntaxFacts.GetKeywordKind(parameterName) != SyntaxKind.None;
    }

    private static Location GetMemberLocation(ISymbol member, Location fallback) =>
        member.Locations.FirstOrDefault(location => location.IsInSource) ?? fallback;

    private static string[] AllocateUniqueParameterNames(params string[] preferredNames)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var allocated = new string[preferredNames.Length];
        for (var i = 0; i < preferredNames.Length; i++)
        {
            var baseName = preferredNames[i];
            var name = baseName;
            var suffix = 2;
            while (!used.Add(name))
            {
                name = $"{baseName}{suffix++}";
            }

            allocated[i] = name;
        }

        return allocated;
    }

    private static string Pluralize(string singular)
    {
        if (
            singular.Length >= 2
            && singular.EndsWith("y", StringComparison.OrdinalIgnoreCase)
            && char.IsLetter(singular[singular.Length - 2])
            && !"aeiou".Contains(char.ToLowerInvariant(singular[singular.Length - 2]))
        )
        {
            return singular.Substring(0, singular.Length - 1) + "ies";
        }

        if (
            singular.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("x", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("z", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            || singular.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
        )
        {
            return singular + "es";
        }

        return singular + "s";
    }

    private static string CamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);

    private static string EscapeIdentifier(string identifier) =>
        SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None ? identifier : "@" + identifier;

    private static string GetAccessibility(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };

    private enum BlueprintKind
    {
        Root,
        RootInput,
        Child,
        ChildInput,
    }

    private sealed class BlueprintMemberModel(
        string sourceName,
        string name,
        string plural,
        string escapedSourceName,
        string seedType,
        string? parentType,
        string? inputType,
        string outputType,
        BlueprintKind kind,
        string accessibility,
        bool isValidName,
        bool isValidPlural,
        bool hasPluralOverride,
        SourceLocationModel location
    ) : IEquatable<BlueprintMemberModel>
    {
        public string SourceName { get; } = sourceName;
        public string Name { get; } = name;
        public string Plural { get; } = plural;
        public string EscapedSourceName { get; } = escapedSourceName;
        public string SeedType { get; } = seedType;
        public string? ParentType { get; } = parentType;
        public string? InputType { get; } = inputType;
        public string OutputType { get; } = outputType;
        public BlueprintKind Kind { get; } = kind;
        public string Accessibility { get; } = accessibility;
        public bool IsValidName { get; } = isValidName;
        public bool IsValidPlural { get; } = isValidPlural;
        public bool HasPluralOverride { get; } = hasPluralOverride;
        public bool IsValid => IsValidName && IsValidPlural;
        public SourceLocationModel Location { get; } = location;

        public bool Equals(BlueprintMemberModel? other) =>
            other is not null
            && SourceName == other.SourceName
            && Name == other.Name
            && Plural == other.Plural
            && EscapedSourceName == other.EscapedSourceName
            && SeedType == other.SeedType
            && ParentType == other.ParentType
            && InputType == other.InputType
            && OutputType == other.OutputType
            && Kind == other.Kind
            && Accessibility == other.Accessibility
            && IsValidName == other.IsValidName
            && IsValidPlural == other.IsValidPlural
            && HasPluralOverride == other.HasPluralOverride
            && Location.Equals(other.Location);

        public override bool Equals(object? obj) =>
            obj is BlueprintMemberModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourceName.GetHashCode();
                hash = (hash * 397) ^ Name.GetHashCode();
                hash = (hash * 397) ^ Plural.GetHashCode();
                hash = (hash * 397) ^ EscapedSourceName.GetHashCode();
                hash = (hash * 397) ^ SeedType.GetHashCode();
                hash = (hash * 397) ^ (ParentType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ (InputType?.GetHashCode() ?? 0);
                hash = (hash * 397) ^ OutputType.GetHashCode();
                hash = (hash * 397) ^ (int)Kind;
                hash = (hash * 397) ^ Accessibility.GetHashCode();
                hash = (hash * 397) ^ IsValidName.GetHashCode();
                hash = (hash * 397) ^ IsValidPlural.GetHashCode();
                hash = (hash * 397) ^ HasPluralOverride.GetHashCode();
                return (hash * 397) ^ Location.GetHashCode();
            }
        }
    }

    private sealed class CatalogModel(
        string name,
        string qualifiedName,
        string @namespace,
        string accessibility,
        bool isSupported,
        SourceLocationModel location,
        ImmutableArray<BlueprintMemberModel> members
    ) : IEquatable<CatalogModel>
    {
        public string Name { get; } = name;
        public string QualifiedName { get; } = qualifiedName;
        public string Namespace { get; } = @namespace;
        public string Accessibility { get; } = accessibility;
        public bool IsSupported { get; } = isSupported;
        public SourceLocationModel Location { get; } = location;
        public ImmutableArray<BlueprintMemberModel> Members { get; } = members;

        public bool Equals(CatalogModel? other) =>
            other is not null
            && Name == other.Name
            && QualifiedName == other.QualifiedName
            && Namespace == other.Namespace
            && Accessibility == other.Accessibility
            && IsSupported == other.IsSupported
            && Location.Equals(other.Location)
            && Members.SequenceEqual(other.Members);

        public override bool Equals(object? obj) => obj is CatalogModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Name.GetHashCode();
                hash = (hash * 397) ^ QualifiedName.GetHashCode();
                hash = (hash * 397) ^ Namespace.GetHashCode();
                hash = (hash * 397) ^ Accessibility.GetHashCode();
                hash = (hash * 397) ^ IsSupported.GetHashCode();
                hash = (hash * 397) ^ Location.GetHashCode();
                foreach (var member in Members)
                {
                    hash = (hash * 397) ^ member.GetHashCode();
                }
                return hash;
            }
        }
    }

    private sealed class MethodModel(string name, string[] parameters)
    {
        public string Name { get; } = name;
        public string[] Parameters { get; } = parameters;
        public string DisplaySignature => $"{Name}({string.Join(", ", Parameters)})";
    }

    private sealed class GeneratedSourceModel(string hintName, string source)
        : IEquatable<GeneratedSourceModel>
    {
        public string HintName { get; } = hintName;
        public string Source { get; } = source;

        public bool Equals(GeneratedSourceModel? other) =>
            other is not null && HintName == other.HintName && Source == other.Source;

        public override bool Equals(object? obj) =>
            obj is GeneratedSourceModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (HintName.GetHashCode() * 397) ^ Source.GetHashCode();
            }
        }
    }

    private sealed class SourceLocationModel(
        string filePath,
        int start,
        int length,
        int startLine,
        int startCharacter,
        int endLine,
        int endCharacter,
        int syntaxTreeIndex
    ) : IEquatable<SourceLocationModel>
    {
        public string FilePath { get; } = filePath;
        public int Start { get; } = start;
        public int Length { get; } = length;
        public int StartLine { get; } = startLine;
        public int StartCharacter { get; } = startCharacter;
        public int EndLine { get; } = endLine;
        public int EndCharacter { get; } = endCharacter;
        public int SyntaxTreeIndex { get; } = syntaxTreeIndex;

        public static SourceLocationModel From(Location location, Compilation compilation)
        {
            var lineSpan = location.GetLineSpan();
            var syntaxTreeIndex = -1;
            if (location.SourceTree is { } sourceTree)
            {
                var index = 0;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    if (ReferenceEquals(syntaxTree, sourceTree))
                    {
                        syntaxTreeIndex = index;
                        break;
                    }

                    index++;
                }
            }

            return new SourceLocationModel(
                lineSpan.Path,
                location.SourceSpan.Start,
                location.SourceSpan.Length,
                lineSpan.Span.Start.Line,
                lineSpan.Span.Start.Character,
                lineSpan.Span.End.Line,
                lineSpan.Span.End.Character,
                syntaxTreeIndex
            );
        }

        public Location ToLocation(Compilation compilation)
        {
            if (SyntaxTreeIndex >= 0)
            {
                var index = 0;
                foreach (var syntaxTree in compilation.SyntaxTrees)
                {
                    if (index == SyntaxTreeIndex)
                    {
                        return Location.Create(syntaxTree, new TextSpan(Start, Length));
                    }

                    index++;
                }
            }

            return Location.Create(
                FilePath,
                new TextSpan(Start, Length),
                new LinePositionSpan(
                    new LinePosition(StartLine, StartCharacter),
                    new LinePosition(EndLine, EndCharacter)
                )
            );
        }

        public bool Equals(SourceLocationModel? other) =>
            other is not null
            && FilePath == other.FilePath
            && Start == other.Start
            && Length == other.Length
            && StartLine == other.StartLine
            && StartCharacter == other.StartCharacter
            && EndLine == other.EndLine
            && EndCharacter == other.EndCharacter
            && SyntaxTreeIndex == other.SyntaxTreeIndex;

        public override bool Equals(object? obj) =>
            obj is SourceLocationModel other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = FilePath.GetHashCode();
                hash = (hash * 397) ^ Start;
                hash = (hash * 397) ^ Length;
                hash = (hash * 397) ^ StartLine;
                hash = (hash * 397) ^ StartCharacter;
                hash = (hash * 397) ^ EndLine;
                hash = (hash * 397) ^ EndCharacter;
                return (hash * 397) ^ SyntaxTreeIndex;
            }
        }
    }
}
