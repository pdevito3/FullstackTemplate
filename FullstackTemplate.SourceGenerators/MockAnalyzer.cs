namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Refuses a substitute of a concrete class or of a DbContext. A substitute of a class runs the real code of each member
/// that is not virtual, so the test checks half a fake. A DbContext needs the real database. See "Tests" in
/// docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MockAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor ConcreteSubstitute = new(
		id: "FSTMCK001",
		title: "A test substitutes a concrete class or a DbContext",
		messageFormat: "Substitute.{0}<{1}> fakes {2}. Substitute an interface or an abstract class, or use the real type. See docs/backend/code-style.md.",
		category: "Tests",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ConcreteSubstitute];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterOperationAction(Analyze, OperationKind.Invocation);
	}

	private static void Analyze(OperationAnalysisContext ctx)
	{
		var method = ((IInvocationOperation)ctx.Operation).TargetMethod;
		if (method.Name is not ("For" or "ForPartsOf") || method.ContainingType?.ToDisplayString() != "NSubstitute.Substitute")
			return;

		foreach (var type in method.TypeArguments)
		{
			var what = IsDbContext(type) ? "a DbContext"
				: type is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } ? "a concrete class"
				: null;
			if (what is not null)
				ctx.ReportDiagnostic(Diagnostic.Create(ConcreteSubstitute, ctx.Operation.Syntax.GetLocation(), method.Name, type.Name, what));
		}
	}

	private static bool IsDbContext(ITypeSymbol type)
	{
		for (var current = type.BaseType; current is not null; current = current.BaseType)
		{
			if (current.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbContext")
				return true;
		}

		return false;
	}
}
