namespace FullstackTemplate.SourceGenerators;

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
		[MissingPrefix, InvalidPrefix, ConstructorPrefix, EntityInheritance];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (EntityTypes.From(start.Compilation) is not { } entities)
				return;

			// A symbol action runs once for each type, also for a partial class that is declared in more than one file.
			start.RegisterSymbolAction(ctx => AnalyzeType(ctx, entities), SymbolKind.NamedType);

			// A constructor is declared once, so a syntax action checks each constructor once.
			start.RegisterSyntaxNodeAction(ctx => AnalyzeConstructor(ctx, entities), SyntaxKind.ConstructorDeclaration);
			start.RegisterSyntaxNodeAction(ctx => AnalyzePrimaryConstructor(ctx, entities), SyntaxKind.ClassDeclaration);
		});
	}

	private static void AnalyzeType(SymbolAnalysisContext ctx, EntityTypes entities)
	{
		var symbol = (INamedTypeSymbol)ctx.Symbol;
		if (!entities.IsEntity(symbol))
			return;

		var typeName = symbol.ToDisplayString();
		var classLocation = symbol.Locations.FirstOrDefault() ?? Location.None;
		var prefixField = symbol.IsAbstract ? null : EntityTypes.FindPrefixField(symbol);

		// An implicit constructor calls base() with no prefix. Without a prefix, FSTID001 is the only useful error.
		if ((symbol.IsAbstract || prefixField is not null) && symbol.InstanceConstructors.Any(static c => c.IsImplicitlyDeclared))
			ctx.ReportDiagnostic(Diagnostic.Create(ConstructorPrefix, classLocation, typeName, symbol.IsAbstract ? PrefixParameterText : OwnPrefixText));

		if (symbol.IsAbstract)
			return;

		var entityBase = entities.FindEntityBase(symbol);
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
	}

	/// <summary>
	/// Checks that a constructor gives BaseEntity the right prefix. A concrete entity must pass its own IdPrefix field.
	/// An abstract base class must pass a parameter of the same constructor that is named idPrefix.
	/// A constructor that calls this(...) is not checked, because the target constructor is checked.
	/// </summary>
	private static void AnalyzeConstructor(SyntaxNodeAnalysisContext ctx, EntityTypes entities)
	{
		var decl = (ConstructorDeclarationSyntax)ctx.Node;
		if (decl.Modifiers.Any(SyntaxKind.StaticKeyword))
			return;

		if (ctx.SemanticModel.GetDeclaredSymbol(decl, ctx.CancellationToken) is not { } ctor
			|| !TryGetExpectedPrefix(ctor.ContainingType, entities, out var prefixField))
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
	private static void AnalyzePrimaryConstructor(SyntaxNodeAnalysisContext ctx, EntityTypes entities)
	{
		var decl = (ClassDeclarationSyntax)ctx.Node;
		if (decl.ParameterList is null)
			return;

		if (ctx.SemanticModel.GetDeclaredSymbol(decl, ctx.CancellationToken) is not { } symbol
			|| !TryGetExpectedPrefix(symbol, entities, out var prefixField))
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
	private static bool TryGetExpectedPrefix(INamedTypeSymbol symbol, EntityTypes entities, out IFieldSymbol? prefixField)
	{
		prefixField = null;
		if (!entities.IsEntity(symbol))
			return false;
		if (symbol.IsAbstract)
			return true;

		prefixField = EntityTypes.FindPrefixField(symbol);
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
			_ => [],
		};

		var value = arguments.FirstOrDefault(static a => a.Parameter?.Name == PrefixParameterName)?.Value;
		while (value is IConversionOperation { IsImplicit: true } conversion)
			value = conversion.Operand;

		return prefixField is null
			? value is IParameterReferenceOperation { Parameter: { Name: PrefixParameterName } parameter }
				&& SymbolEqualityComparer.Default.Equals(parameter.ContainingSymbol, ctor)
			: value is IFieldReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Field, prefixField);
	}

	internal static bool IsValidPrefix(string prefix)
		=> prefix.Length is > 0 and <= MaxPrefixLength && prefix.All(static c => c is >= 'a' and <= 'z');
}
