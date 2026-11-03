namespace FullstackTemplate.SourceGenerators;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

// Only this rule waits for the end of the compilation. An IDE that runs an analyzer with such a rule only in a build still runs FSTID001, 002, 004, and 005 as you type
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateEntityIdPrefixAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor DuplicatePrefix = new(
		id: "FSTID003",
		title: "Entity IdPrefix is not unique",
		messageFormat: "Entity '{0}' has IdPrefix '{1}', which entity '{2}' also uses",
		category: "EntityId",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: WellKnownDiagnosticTags.CompilationEnd);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DuplicatePrefix];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (EntityTypes.From(start.Compilation) is not { } entities)
				return;

			var prefixes = new ConcurrentBag<EntityPrefix>();
			start.RegisterSymbolAction(ctx => Record(ctx, entities, prefixes), SymbolKind.NamedType);
			start.RegisterCompilationEndAction(end => ReportDuplicates(end, prefixes));
		});
	}

	private static void Record(SymbolAnalysisContext ctx, EntityTypes entities, ConcurrentBag<EntityPrefix> prefixes)
	{
		var symbol = (INamedTypeSymbol)ctx.Symbol;
		if (symbol.IsAbstract || !entities.IsEntity(symbol)
			|| EntityTypes.FindPrefixField(symbol) is not { } prefixField)
			return;

		var location = prefixField.Locations.FirstOrDefault() ?? symbol.Locations.FirstOrDefault() ?? Location.None;
		prefixes.Add(new EntityPrefix(symbol.ToDisplayString(), prefixField.ConstantValue as string ?? "", location));
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

	private sealed record EntityPrefix(string TypeName, string Prefix, Location Location);
}
