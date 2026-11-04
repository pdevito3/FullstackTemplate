namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryTagAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor UntaggedQuery = new(
		id: "FSTTEL001",
		title: "An EF query has no operation tag",
		messageFormat: "The query on '{0}' has no TagWithOperationCallSite. Put .TagWithOperationCallSite(\"area.operation\") directly after '{0}'. See docs/backend/feature-telemetry.md.",
		category: "Telemetry",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UntaggedQuery];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (QueryChains.From(start.Compilation) is { } chains)
				start.RegisterOperationAction(ctx => Analyze(ctx, chains), OperationKind.PropertyReference, OperationKind.Invocation);
		});
	}

	private static void Analyze(OperationAnalysisContext ctx, QueryChains chains)
	{
		var operation = ctx.Operation;
		if (chains.DbSetName(operation) is { } name
			&& !chains.InExpressionTree(operation)
			&& chains.Classify(operation, ctx.ContainingSymbol).Use == QueryUse.Untagged)
			ctx.ReportDiagnostic(Diagnostic.Create(UntaggedQuery, operation.Syntax.GetLocation(), name));
	}
}
