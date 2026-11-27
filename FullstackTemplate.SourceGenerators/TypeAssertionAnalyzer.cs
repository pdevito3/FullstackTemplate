namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Refuses the ways to lose a type and claim it back: an alias of object, a cast of a cast, and a local that widens a
/// value to object and casts it back. See "Claims the compiler cannot check" in docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeAssertionAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor ObjectAlias = new(
		id: "FSTTYP001",
		title: "A using alias names object",
		messageFormat: "'{0}' gives object a name, so a signature that uses it looks typed and is not. Use the real type. See docs/backend/code-style.md.",
		category: "Types",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor ChainedCast = new(
		id: "FSTTYP002",
		title: "A cast of a cast",
		messageFormat: "'{0}' casts a cast, which forces a conversion that the compiler refuses. Give the value the right type where it is made. See docs/backend/code-style.md.",
		category: "Types",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor WidenThenAssert = new(
		id: "FSTTYP003",
		title: "A local widens a value to object and casts it back",
		messageFormat: "The local '{0}' holds a {1} as object, and '{2}' takes the type back. Keep the local as {1}. See docs/backend/code-style.md.",
		category: "Types",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ObjectAlias, ChainedCast, WidenThenAssert];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeAlias, SyntaxKind.UsingDirective);
		context.RegisterSyntaxNodeAction(AnalyzeChain, SyntaxKind.CastExpression, SyntaxKind.AsExpression);
		context.RegisterOperationAction(AnalyzeBody, OperationKind.MethodBody, OperationKind.ConstructorBody);
	}

	private static void AnalyzeAlias(SyntaxNodeAnalysisContext ctx)
	{
		var directive = (UsingDirectiveSyntax)ctx.Node;
		if (directive.Alias is null || directive.NamespaceOrType is null)
			return;

		if (ctx.SemanticModel.GetTypeInfo(directive.NamespaceOrType, ctx.CancellationToken).Type?.SpecialType == SpecialType.System_Object)
			ctx.ReportDiagnostic(Diagnostic.Create(ObjectAlias, directive.GetLocation(), directive.ToString().Trim()));
	}

	private static void AnalyzeChain(SyntaxNodeAnalysisContext ctx)
	{
		var inner = ctx.Node is CastExpressionSyntax cast ? cast.Expression : ((BinaryExpressionSyntax)ctx.Node).Left;
		while (inner is ParenthesizedExpressionSyntax parenthesized)
			inner = parenthesized.Expression;

		var (innerOperand, innerTarget) = inner switch
		{
			CastExpressionSyntax innerCast => (innerCast.Expression, innerCast.Type),
			BinaryExpressionSyntax innerAs when innerAs.IsKind(SyntaxKind.AsExpression) => (innerAs.Left, (ExpressionSyntax)innerAs.Right),
			_ => (null, null),
		};
		if (innerOperand is null || innerTarget is null)
			return;

		// Only a value that is widened to a reference type and then cast back down. A numeric cast of a
		// numeric cast is a conversion, and (T)(object)value is the C# form of a cast to a type parameter.
		var model = ctx.SemanticModel;
		var source = model.GetTypeInfo(innerOperand, ctx.CancellationToken).Type;
		var widened = model.GetTypeInfo(innerTarget, ctx.CancellationToken).Type;
		var outer = model.GetTypeInfo(ctx.Node, ctx.CancellationToken).Type;
		if (source is null || widened is null || outer is null
			|| !widened.IsReferenceType
			|| source.TypeKind == TypeKind.TypeParameter || outer.TypeKind == TypeKind.TypeParameter
			|| SymbolEqualityComparer.Default.Equals(source, widened)
			|| !model.Compilation.ClassifyCommonConversion(source, widened).IsImplicit
			|| model.Compilation.ClassifyCommonConversion(widened, outer).IsImplicit)
			return;

		ctx.ReportDiagnostic(Diagnostic.Create(ChainedCast, ctx.Node.GetLocation(), ctx.Node.ToString()));
	}

	private static void AnalyzeBody(OperationAnalysisContext ctx)
	{
		var operations = ctx.Operation.Descendants().ToList();
		var widened = operations.OfType<IVariableDeclaratorOperation>()
			.Where(declarator => declarator.Symbol.Type.SpecialType == SpecialType.System_Object
				&& declarator.Initializer?.Value is IConversionOperation { Operand.Type: { SpecialType: not SpecialType.System_Object } })
			.ToList();

		foreach (var declarator in widened)
		{
			var local = declarator.Symbol;
			var assertion = operations.FirstOrDefault(operation => operation switch
			{
				IConversionOperation { IsImplicit: false, Operand: ILocalReferenceOperation reference } => Is(reference, local),
				IIsPatternOperation { Value: ILocalReferenceOperation reference, Pattern: var pattern } => Is(reference, local) && NamesType(pattern),
				IIsTypeOperation { ValueOperand: ILocalReferenceOperation reference, TypeOperand.SpecialType: not SpecialType.System_Object } => Is(reference, local),
				_ => false,
			});
			if (assertion is null)
				continue;

			var source = ((IConversionOperation)declarator.Initializer!.Value).Operand.Type!;
			ctx.ReportDiagnostic(Diagnostic.Create(WidenThenAssert, assertion.Syntax.GetLocation(),
				local.Name, source.ToDisplayString(), assertion.Syntax.ToString()));
		}
	}

	/// <summary>A pattern that tests a type, not a null test or a constant.</summary>
	private static bool NamesType(IPatternOperation pattern) => pattern switch
	{
		IDeclarationPatternOperation { MatchesNull: false } declaration => declaration.NarrowedType.SpecialType != SpecialType.System_Object,
		ITypePatternOperation type => type.NarrowedType.SpecialType != SpecialType.System_Object,
		IRecursivePatternOperation recursive => recursive.MatchedType.SpecialType != SpecialType.System_Object,
		INegatedPatternOperation negated => NamesType(negated.Pattern),
		IBinaryPatternOperation binary => NamesType(binary.LeftPattern) || NamesType(binary.RightPattern),
		_ => false,
	};

	private static bool Is(ILocalReferenceOperation reference, ILocalSymbol local) =>
		SymbolEqualityComparer.Default.Equals(reference.Local, local);
}
