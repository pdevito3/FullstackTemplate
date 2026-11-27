namespace FullstackTemplate.SourceGenerators;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Refuses a branch that the language has a form for: a catch of Exception that tests the type of the exception and
/// throws the rest again, and a chain of ternaries that tests one value. See "Branches" in docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BranchAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor CatchTypeTest = new(
		id: "FSTBRN001",
		title: "A catch of Exception tests the type and throws the rest again",
		messageFormat: "'{0}' picks the exceptions that a catch of Exception handles, and throw; passes the rest on. Catch the type, or add a when filter. See docs/backend/code-style.md.",
		category: "Branches",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor TernaryChain = new(
		id: "FSTBRN002",
		title: "A chain of ternaries tests one value",
		messageFormat: "The ternaries test '{0}' {1} times. Write a switch expression on it. See docs/backend/code-style.md.",
		category: "Branches",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [CatchTypeTest, TernaryChain];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSyntaxNodeAction(AnalyzeCatch, SyntaxKind.CatchClause);
		context.RegisterSyntaxNodeAction(AnalyzeTernary, SyntaxKind.ConditionalExpression);
	}

	private static void AnalyzeCatch(SyntaxNodeAnalysisContext ctx)
	{
		var clause = (CatchClauseSyntax)ctx.Node;
		if (clause.Filter is not null || clause.Declaration is not { Identifier.ValueText: { Length: > 0 } name } declaration)
			return;
		if (ctx.SemanticModel.GetTypeInfo(declaration.Type, ctx.CancellationToken).Type?.ToDisplayString() != "System.Exception")
			return;

		// Only the statements of the catch block, not of a nested lambda or block, decide what the catch handles
		var statements = clause.Block.Statements;
		var test = statements.OfType<IfStatementSyntax>().FirstOrDefault(statement => TestsTypeOf(statement.Condition, name));
		if (test is null)
			return;

		// The type picks what the catch handles when one branch of the if rethrows, or when the branch of the type leaves
		// the catch and a throw; after the if rethrows the rest. A catch that runs the if and then rethrows each exception
		// handles nothing, so a filter would change what it does.
		var rethrowsAfter = statements.SkipWhile(statement => statement != test).Skip(1).Any(IsRethrow);
		var picks = IsRethrow(test.Statement)
			|| ElseRethrows(test)
			|| (Leaves(test.Statement) && rethrowsAfter);
		if (picks)
			ctx.ReportDiagnostic(Diagnostic.Create(CatchTypeTest, test.Condition.GetLocation(), test.Condition.ToString()));
	}

	private static bool TestsTypeOf(ExpressionSyntax condition, string name)
	{
		condition = Unwrap(condition);
		return condition switch
		{
			IsPatternExpressionSyntax pattern => IsName(pattern.Expression, name) && NamesType(pattern.Pattern),
			BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) => IsName(binary.Left, name),
			PrefixUnaryExpressionSyntax negation when negation.IsKind(SyntaxKind.LogicalNotExpression) => TestsTypeOf(negation.Operand, name),
			BinaryExpressionSyntax both when both.IsKind(SyntaxKind.LogicalAndExpression) => TestsTypeOf(both.Left, name),
			_ => false,
		};
	}

	private static bool NamesType(PatternSyntax pattern) => pattern switch
	{
		DeclarationPatternSyntax or TypePatternSyntax => true,
		RecursivePatternSyntax recursive => recursive.Type is not null,
		UnaryPatternSyntax unary => NamesType(unary.Pattern),
		BinaryPatternSyntax binary => NamesType(binary.Left) || NamesType(binary.Right),
		ConstantPatternSyntax constant => constant.Expression is not LiteralExpressionSyntax,
		_ => false,
	};

	private static bool IsName(ExpressionSyntax expression, string name) =>
		Unwrap(expression) is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == name;

	/// <summary>A statement that leaves the catch block: a return, a break, a continue, or a throw.</summary>
	private static bool Leaves(StatementSyntax statement) => statement switch
	{
		ReturnStatementSyntax or BreakStatementSyntax or ContinueStatementSyntax or ThrowStatementSyntax => true,
		BlockSyntax { Statements.Count: > 0 } block => Leaves(block.Statements[block.Statements.Count - 1]),
		_ => false,
	};

	/// <summary>The last else of an if, or of its chain of else ifs, rethrows.</summary>
	private static bool ElseRethrows(IfStatementSyntax test) => test.Else?.Statement switch
	{
		IfStatementSyntax next => ElseRethrows(next),
		{ } last => IsRethrow(last),
		null => false,
	};

	private static bool IsRethrow(StatementSyntax statement) => statement switch
	{
		ThrowStatementSyntax { Expression: null } => true,
		BlockSyntax { Statements.Count: > 0 } block => IsRethrow(block.Statements[block.Statements.Count - 1]),
		_ => false,
	};

	private static void AnalyzeTernary(SyntaxNodeAnalysisContext ctx)
	{
		var ternary = (ConditionalExpressionSyntax)ctx.Node;

		// Report the head of a chain one time, not each link of it
		var above = ternary.Parent;
		while (above is ParenthesizedExpressionSyntax)
			above = above.Parent;
		if (above is ConditionalExpressionSyntax parent && Unwrap(parent.WhenFalse) == ternary)
			return;

		var subjects = new List<ExpressionSyntax>();
		for (ExpressionSyntax? link = ternary; link is ConditionalExpressionSyntax conditional; link = Unwrap(conditional.WhenFalse))
		{
			if (SubjectOf(conditional.Condition, ctx.SemanticModel) is not { } subject)
				return;
			subjects.Add(subject);
		}

		if (subjects.Count >= 2 && subjects.All(subject => SyntaxFactory.AreEquivalent(subject, subjects[0])))
			ctx.ReportDiagnostic(Diagnostic.Create(TernaryChain, ternary.GetLocation(), subjects[0].ToString(), subjects.Count));
	}

	/// <summary>The value that a condition compares to a constant or matches with a pattern, or null.</summary>
	private static ExpressionSyntax? SubjectOf(ExpressionSyntax condition, SemanticModel model)
	{
		condition = Unwrap(condition);
		if (condition is IsPatternExpressionSyntax pattern)
			return pattern.Expression;
		if (condition is not BinaryExpressionSyntax binary || !binary.IsKind(SyntaxKind.EqualsExpression))
			return null;
		if (model.GetConstantValue(binary.Right).HasValue)
			return binary.Left;
		if (model.GetConstantValue(binary.Left).HasValue)
			return binary.Right;
		return null;
	}

	private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
	{
		while (expression is ParenthesizedExpressionSyntax parenthesized)
			expression = parenthesized.Expression;
		return expression;
	}
}
