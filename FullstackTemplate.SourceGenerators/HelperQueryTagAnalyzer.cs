namespace FullstackTemplate.SourceGenerators;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

// Only this rule waits for the end of the compilation. An IDE that runs an analyzer with such a rule only in a build still runs FSTTEL001 as you type
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HelperQueryTagAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor UntaggedHelperQuery = new(
		id: "FSTTEL003",
		title: "A caller of a method that returns an untagged query does not tag it",
		messageFormat: "'{0}' returns a query with no TagWithOperationCallSite, and this caller does not tag it. Put .TagWithOperationCallSite(\"area.operation\") directly after '{0}'. See docs/backend/feature-telemetry.md.",
		category: "Telemetry",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: WellKnownDiagnosticTags.CompilationEnd);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UntaggedHelperQuery];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (QueryChains.From(start.Compilation) is not { } chains)
				return;

			var helpers = new HelperQueries(chains);
			start.RegisterOperationAction(helpers.Record, OperationKind.PropertyReference, OperationKind.Invocation);
			start.RegisterCompilationEndAction(helpers.Report);
		});
	}

	private sealed class HelperQueries(QueryChains chains)
	{
		private readonly ConcurrentDictionary<ISymbol, byte> _returnsUntagged = new(SymbolEqualityComparer.Default);
		private readonly ConcurrentBag<HelperCall> _calls = [];

		public void Record(OperationAnalysisContext ctx)
		{
			var operation = ctx.Operation;
			if (chains.InExpressionTree(operation))
				return;

			if (chains.DbSetName(operation) is not null)
			{
				if (chains.Classify(operation, ctx.ContainingSymbol) is (QueryUse.Returned, { } owner))
					_returnsUntagged.TryAdd(owner, 0);
			}
			else if (chains.HelperOf(operation) is { } helper)
			{
				var (use, owner) = chains.Classify(operation, ctx.ContainingSymbol);
				if (use is QueryUse.Untagged or QueryUse.Returned)
					_calls.Add(new HelperCall(helper, use, owner, operation.Syntax.GetLocation()));
			}
		}

		public void Report(CompilationAnalysisContext ctx)
		{
			var returnsUntagged = HelpersThatReturnUntaggedQueries();
			foreach (var call in _calls.Where(c => c.Use == QueryUse.Untagged && returnsUntagged.Contains(c.Helper)))
				ctx.ReportDiagnostic(Diagnostic.Create(UntaggedHelperQuery, call.Location, call.Helper.Name + "()"));
		}

		private HashSet<ISymbol> HelpersThatReturnUntaggedQueries()
		{
			var helpers = new HashSet<ISymbol>(_returnsUntagged.Keys, SymbolEqualityComparer.Default);
			var returned = _calls.Where(static c => c.Use == QueryUse.Returned).ToList();
			while (returned.FirstOrDefault(c => helpers.Contains(c.Helper) && !helpers.Contains(c.Owner!)) is { } call)
				helpers.Add(call.Owner!);

			return helpers;
		}
	}

	private sealed record HelperCall(ISymbol Helper, QueryUse Use, ISymbol? Owner, Location Location);
}
