namespace FullstackTemplate.SourceGenerators;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Checks the <c>IdPrefix</c> constant and the constructors of each entity at compile time.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityIdPrefixAnalyzer : DiagnosticAnalyzer
{
	private const string BaseEntityName = "FullstackTemplate.Server.Domain.BaseEntity";
	private const string PrefixName = "IdPrefix";
	private const string PrefixParameterName = "idPrefix";

	// EntityId.MaxLength is 50: the prefix, "_", and 32 hex characters. A unit test links the two values.
	internal const int MaxPrefixLength = 17;

	internal static readonly DiagnosticDescriptor MissingPrefix = new(
		id: "FSTID001",
		title: "Entity has no IdPrefix",
		messageFormat: "Entity '{0}' must declare 'public const string IdPrefix'",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor InvalidPrefix = new(
		id: "FSTID002",
		title: "Entity IdPrefix is not valid",
		messageFormat: "Entity '{0}' has IdPrefix '{1}'. An IdPrefix must have 1 to " + MaxPrefixLength + " lower-case ASCII letters.",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	// The check needs all entities, so it runs at the end of the compilation.
	internal static readonly DiagnosticDescriptor DuplicatePrefix = new(
		id: "FSTID003",
		title: "Entity IdPrefix is not unique",
		messageFormat: "Entity '{0}' has IdPrefix '{1}', which entity '{2}' also uses",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: WellKnownDiagnosticTags.CompilationEnd);

	internal static readonly DiagnosticDescriptor ConstructorPrefix = new(
		id: "FSTID004",
		title: "Entity constructor does not pass the IdPrefix",
		messageFormat: "A constructor of '{0}' must pass {1} to the idPrefix parameter of the base constructor",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	internal static readonly DiagnosticDescriptor EntityInheritance = new(
		id: "FSTID005",
		title: "Entity derives from another entity",
		messageFormat: "Entity '{0}' derives from '{1}'. An entity must derive from BaseEntity, or from an abstract base class that has no IdPrefix.",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string OwnPrefixText = "its own IdPrefix";
	private const string PrefixParameterText = "its own idPrefix parameter";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		ImmutableArray.Create(MissingPrefix, InvalidPrefix, DuplicatePrefix, ConstructorPrefix, EntityInheritance);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			var baseEntity = start.Compilation.GetTypeByMetadataName(BaseEntityName);
			if (baseEntity is null)
				return;

			var prefixes = new ConcurrentBag<EntityPrefix>();

			// A symbol action runs once for each type, also for a partial class that is declared in more than one file.
			start.RegisterSymbolAction(ctx => AnalyzeType(ctx, baseEntity, prefixes), SymbolKind.NamedType);

			// A constructor is declared once, so a syntax action checks each constructor once.
			start.RegisterSyntaxNodeAction(ctx => AnalyzeConstructor(ctx, baseEntity), SyntaxKind.ConstructorDeclaration);
			start.RegisterSyntaxNodeAction(ctx => AnalyzePrimaryConstructor(ctx, baseEntity), SyntaxKind.ClassDeclaration);

			start.RegisterCompilationEndAction(end => ReportDuplicates(end, prefixes));
		});
	}

	private static void AnalyzeType(SymbolAnalysisContext ctx, INamedTypeSymbol baseEntity, ConcurrentBag<EntityPrefix> prefixes)
	{
		var symbol = (INamedTypeSymbol)ctx.Symbol;
		if (symbol.TypeKind != TypeKind.Class || !InheritsFrom(symbol, baseEntity))
			return;

		var typeName = symbol.ToDisplayString();
		var classLocation = symbol.Locations.FirstOrDefault() ?? Location.None;
		var prefixField = symbol.IsAbstract ? null : FindPrefixField(symbol);

		// An implicit constructor calls base() with no prefix. Without a prefix, FSTID001 is the only useful error.
		if ((symbol.IsAbstract || prefixField is not null) && symbol.InstanceConstructors.Any(static c => c.IsImplicitlyDeclared))
			ctx.ReportDiagnostic(Diagnostic.Create(ConstructorPrefix, classLocation, typeName, symbol.IsAbstract ? PrefixParameterText : OwnPrefixText));

		if (symbol.IsAbstract)
			return;

		var entityBase = FindEntityBase(symbol, baseEntity);
		if (entityBase is not null)
			ctx.ReportDiagnostic(Diagnostic.Create(EntityInheritance, classLocation, typeName, entityBase));

		if (prefixField is null)
		{
			ctx.ReportDiagnostic(Diagnostic.Create(MissingPrefix, classLocation, typeName));
			return;
		}

		var prefix = prefixField.ConstantValue as string ?? "";
		var location = prefixField.Locations.FirstOrDefault() ?? classLocation;
		if (!IsValidPrefix(prefix))
			ctx.ReportDiagnostic(Diagnostic.Create(InvalidPrefix, location, typeName, prefix));

		prefixes.Add(new EntityPrefix(typeName, prefix, location));
	}

	/// <summary>
	/// Checks that a constructor gives BaseEntity the right prefix. A concrete entity must pass its own IdPrefix field.
	/// An abstract base class must pass a parameter of the same constructor that is named idPrefix.
	/// A constructor that calls this(...) is not checked, because the target constructor is checked.
	/// </summary>
	private static void AnalyzeConstructor(SyntaxNodeAnalysisContext ctx, INamedTypeSymbol baseEntity)
	{
		var decl = (ConstructorDeclarationSyntax)ctx.Node;
		if (decl.Modifiers.Any(SyntaxKind.StaticKeyword))
			return;

		if (ctx.SemanticModel.GetDeclaredSymbol(decl, ctx.CancellationToken) is not { } ctor
			|| !TryGetExpectedPrefix(ctor.ContainingType, baseEntity, out var prefixField))
			return;

		var initializer = decl.Initializer;
		if (initializer is null)
		{
			Report(ctx, ctor.ContainingType, decl.Identifier.GetLocation());
			return;
		}

		if (initializer.ThisOrBaseKeyword.IsKind(SyntaxKind.ThisKeyword))
			return;

		if (!PassesPrefix(ctx.SemanticModel, initializer, ctor, prefixField, ctx.CancellationToken))
			Report(ctx, ctor.ContainingType, initializer.GetLocation());
	}

	/// <summary>
	/// Checks a primary constructor, which passes its arguments in the base list, as in "class User() : BaseEntity(IdPrefix)".
	/// </summary>
	private static void AnalyzePrimaryConstructor(SyntaxNodeAnalysisContext ctx, INamedTypeSymbol baseEntity)
	{
		var decl = (ClassDeclarationSyntax)ctx.Node;
		if (decl.ParameterList is null)
			return;

		if (ctx.SemanticModel.GetDeclaredSymbol(decl, ctx.CancellationToken) is not { } symbol
			|| !TryGetExpectedPrefix(symbol, baseEntity, out var prefixField))
			return;

		var ctor = symbol.InstanceConstructors.FirstOrDefault(c =>
			c.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == decl.SyntaxTree && r.Span == decl.Span));
		if (ctor is null)
			return;

		var baseType = decl.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault();
		if (baseType is null)
			Report(ctx, symbol, decl.Identifier.GetLocation());
		else if (!PassesPrefix(ctx.SemanticModel, baseType, ctor, prefixField, ctx.CancellationToken))
			Report(ctx, symbol, baseType.GetLocation());
	}

	/// <summary>
	/// Returns false when the constructors of the type are not checked: the type is not an entity, or it is a concrete
	/// entity without an IdPrefix (FSTID001 is then the only useful error). For an abstract type, the field is null.
	/// </summary>
	private static bool TryGetExpectedPrefix(INamedTypeSymbol symbol, INamedTypeSymbol baseEntity, out IFieldSymbol? prefixField)
	{
		prefixField = null;
		if (!InheritsFrom(symbol, baseEntity))
			return false;
		if (symbol.IsAbstract)
			return true;

		prefixField = FindPrefixField(symbol);
		return prefixField is not null;
	}

	private static void Report(SyntaxNodeAnalysisContext ctx, INamedTypeSymbol symbol, Location location)
		=> ctx.ReportDiagnostic(Diagnostic.Create(
			ConstructorPrefix, location, symbol.ToDisplayString(), symbol.IsAbstract ? PrefixParameterText : OwnPrefixText));

	/// <summary>
	/// Finds the argument that binds to the idPrefix parameter of the base constructor, so that the position and the
	/// name of the argument do not matter. A base constructor without an idPrefix parameter fails the check.
	/// </summary>
	private static bool PassesPrefix(
		SemanticModel model, SyntaxNode baseCall, IMethodSymbol ctor, IFieldSymbol? prefixField, CancellationToken ct)
	{
		var arguments = model.GetOperation(baseCall, ct) switch
		{
			IInvocationOperation invocation => invocation.Arguments,
			IObjectCreationOperation creation => creation.Arguments,
			IExpressionStatementOperation { Operation: IInvocationOperation invocation } => invocation.Arguments,
			_ => ImmutableArray<IArgumentOperation>.Empty,
		};

		var value = arguments.FirstOrDefault(static a => a.Parameter?.Name == PrefixParameterName)?.Value;
		while (value is IConversionOperation { IsImplicit: true } conversion)
			value = conversion.Operand;

		return prefixField is null
			? value is IParameterReferenceOperation { Parameter: { Name: PrefixParameterName } parameter }
				&& SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, ctor)
			: value is IFieldReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Field, prefixField);
	}

	private static IFieldSymbol? FindPrefixField(INamedTypeSymbol symbol)
		=> symbol.GetMembers(PrefixName)
			.OfType<IFieldSymbol>()
			.FirstOrDefault(static f => f is { IsConst: true, DeclaredAccessibility: Accessibility.Public, Type.SpecialType: SpecialType.System_String });

	/// <summary>
	/// Returns the first base type between the entity and BaseEntity that is concrete or declares an IdPrefix, or null.
	/// </summary>
	private static string? FindEntityBase(INamedTypeSymbol symbol, INamedTypeSymbol baseEntity)
	{
		var current = symbol.BaseType;
		while (current is not null && !SymbolEqualityComparer.Default.Equals(current, baseEntity))
		{
			if (!current.IsAbstract || current.GetMembers(PrefixName).Length > 0)
				return current.ToDisplayString();
			current = current.BaseType;
		}

		return null;
	}

	private static void ReportDuplicates(CompilationAnalysisContext ctx, ConcurrentBag<EntityPrefix> prefixes)
	{
		var groups = prefixes
			.GroupBy(static e => e.Prefix)
			.Where(static g => g.Count() > 1);

		foreach (var group in groups)
		{
			var entities = group.OrderBy(static e => e.TypeName, System.StringComparer.Ordinal).ToArray();
			foreach (var entity in entities)
			{
				var others = string.Join("', '", entities.Where(e => e.TypeName != entity.TypeName).Select(static e => e.TypeName));
				ctx.ReportDiagnostic(Diagnostic.Create(DuplicatePrefix, entity.Location, entity.TypeName, entity.Prefix, others));
			}
		}
	}

	internal static bool IsValidPrefix(string prefix)
		=> prefix.Length is > 0 and <= MaxPrefixLength && prefix.All(static c => c is >= 'a' and <= 'z');

	private static bool InheritsFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
	{
		var current = symbol.BaseType;
		while (current is not null)
		{
			if (SymbolEqualityComparer.Default.Equals(current, baseType))
				return true;
			current = current.BaseType;
		}

		return false;
	}

	private sealed record EntityPrefix(string TypeName, string Prefix, Location Location);
}
