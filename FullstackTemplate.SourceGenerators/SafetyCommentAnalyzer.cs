namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Asks for the reason of each claim that the compiler cannot check: a downcast, a <c>!</c>, a
/// <c>#pragma warning disable</c>, and a <c>[SuppressMessage]</c>. See "Claims the compiler cannot check" in
/// docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SafetyCommentAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor DowncastWithoutReason = new(
		id: "FSTSAF001",
		title: "A downcast has no SAFETY comment",
		messageFormat: "The cast '{0}' can fail at run time. Say why it holds in a // SAFETY: comment above the statement or at the end of the line, or use a type test. See docs/backend/code-style.md.",
		category: "Safety",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AppCode]);

	internal static readonly DiagnosticDescriptor NullForgivingWithoutReason = new(
		id: "FSTSAF002",
		title: "A null-forgiving operator has no SAFETY comment",
		messageFormat: "The ! of '{0}' hides a possible null. Say why it is not null in a // SAFETY: comment above the statement or at the end of the line, or check for null. See docs/backend/code-style.md.",
		category: "Safety",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AppCode]);

	internal static readonly DiagnosticDescriptor PragmaWithoutReason = new(
		id: "FSTSAF003",
		title: "A #pragma warning disable has no SAFETY comment",
		messageFormat: "'{0}' has no reason. Say why the warning does not apply in a // SAFETY: comment above it or at the end of its line. See docs/backend/code-style.md.",
		category: "Safety",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor SuppressionWithoutJustification = new(
		id: "FSTSAF004",
		title: "A SuppressMessage has no Justification",
		messageFormat: "The suppression of {0} has no Justification. Set Justification to why the rule does not apply. See docs/backend/code-style.md.",
		category: "Safety",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	private const string Marker = "// SAFETY:";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
		[DowncastWithoutReason, NullForgivingWithoutReason, PragmaWithoutReason, SuppressionWithoutJustification];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeCast, SyntaxKind.CastExpression);
		context.RegisterSyntaxNodeAction(AnalyzeNullForgiving, SyntaxKind.SuppressNullableWarningExpression);
		context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
		context.RegisterSyntaxTreeAction(AnalyzePragmas);
	}

	private static void AnalyzeCast(SyntaxNodeAnalysisContext ctx)
	{
		var cast = (CastExpressionSyntax)ctx.Node;
		if (ctx.SemanticModel.GetTypeInfo(cast.Type, ctx.CancellationToken).Type is not { } target)
			return;

		// A numeric or a user-defined conversion cannot fail with an InvalidCastException. A reference downcast and an
		// unboxing can.
		var conversion = ctx.SemanticModel.ClassifyConversion(cast.Expression, target, isExplicitInSource: true);
		if (conversion.IsIdentity || !conversion.IsExplicit || !(conversion.IsReference || conversion.IsUnboxing))
			return;

		if (!HasReason(cast))
			ctx.ReportDiagnostic(Diagnostic.Create(DowncastWithoutReason, cast.GetLocation(), cast.ToString()));
	}

	private static void AnalyzeNullForgiving(SyntaxNodeAnalysisContext ctx)
	{
		var bang = (PostfixUnaryExpressionSyntax)ctx.Node;

		// null! and default! give a property or a field a value that the constructor or EF sets. They claim nothing
		// about a value that the code reads.
		if (bang.Operand.Kind() is SyntaxKind.NullLiteralExpression or SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression)
			return;

		if (!HasReason(bang))
			ctx.ReportDiagnostic(Diagnostic.Create(NullForgivingWithoutReason, bang.GetLocation(), bang.Operand.ToString()));
	}

	private static void AnalyzeAttribute(SyntaxNodeAnalysisContext ctx)
	{
		var attribute = (AttributeSyntax)ctx.Node;
		if (ctx.SemanticModel.GetSymbolInfo(attribute, ctx.CancellationToken).Symbol?.ContainingType is not { } type
			|| type.ToDisplayString() is not ("System.Diagnostics.CodeAnalysis.SuppressMessageAttribute"
				or "System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessageAttribute"))
			return;

		var arguments = attribute.ArgumentList?.Arguments ?? default;
		var justification = arguments.FirstOrDefault(a => a.NameEquals?.Name.Identifier.ValueText == "Justification");
		var text = justification is null ? null : ctx.SemanticModel.GetConstantValue(justification.Expression, ctx.CancellationToken).Value as string;
		if (!string.IsNullOrWhiteSpace(text))
			return;

		var rule = arguments.Count > 1 ? arguments[1].Expression.ToString() : "a rule";
		ctx.ReportDiagnostic(Diagnostic.Create(SuppressionWithoutJustification, attribute.GetLocation(), rule));
	}

	private static void AnalyzePragmas(SyntaxTreeAnalysisContext ctx)
	{
		var root = ctx.Tree.GetRoot(ctx.CancellationToken);
		if (!root.ContainsDirectives)
			return;

		var lines = ctx.Tree.GetText(ctx.CancellationToken).Lines;
		foreach (var pragma in root.DescendantNodes(descendIntoTrivia: true).OfType<PragmaWarningDirectiveTriviaSyntax>())
		{
			if (!pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
				continue;

			// The comment at the end of the line, or a line of the comment block right above it
			var line = lines.GetLineFromPosition(pragma.SpanStart).LineNumber;
			var reason = IsReason(lines[line].ToString());
			for (var above = line - 1; !reason && above >= 0 && lines[above].ToString().TrimStart().StartsWith("//"); above--)
				reason = IsReason(lines[above].ToString());

			if (!reason)
				ctx.ReportDiagnostic(Diagnostic.Create(PragmaWithoutReason, pragma.GetLocation(), pragma.ToString().Trim()));
		}
	}

	/// <summary>
	/// A // SAFETY: comment above the node, above an enclosing node up to its statement in a block or its member, or at
	/// the end of a line of the node. So one comment above a statement covers each claim in it, and a comment above an
	/// if with no braces covers the statement under it.
	/// </summary>
	private static bool HasReason(SyntaxNode node)
	{
		for (var current = node; current is not null; current = current.Parent)
		{
			if (current.GetLeadingTrivia().Any(IsReason))
				return true;
			if (current is MemberDeclarationSyntax || (current is StatementSyntax && current.Parent is BlockSyntax or SwitchSectionSyntax or GlobalStatementSyntax))
				break;
		}

		var lines = node.SyntaxTree.GetText().Lines;
		var lastLine = lines.GetLineFromPosition(node.Span.End).LineNumber;
		for (var token = node.GetFirstToken(); token != default && token.SpanStart <= lines[lastLine].End; token = token.GetNextToken())
		{
			if (token.TrailingTrivia.Any(IsReason))
				return true;
		}

		return false;
	}

	private static bool IsReason(SyntaxTrivia trivia) =>
		trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && IsReason(trivia.ToString());

	private static bool IsReason(string text)
	{
		var at = text.IndexOf(Marker, System.StringComparison.Ordinal);
		return at >= 0 && text.Substring(at + Marker.Length).Trim().Length > 0;
	}
}
