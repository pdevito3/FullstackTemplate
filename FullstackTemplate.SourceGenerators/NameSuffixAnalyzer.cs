namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Refuses a type, a method, or a property whose name ends in a word that says nothing about what it holds or does,
/// such as Helper, Util, Info, or Data. Manager is a word of the domain here (a tenant manager), so it is allowed. See
/// "Names" in docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NameSuffixAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor VagueSuffix = new(
		id: "FSTNAM001",
		title: "A name ends in a vague word",
		messageFormat: "The name '{0}' ends in '{1}', which says nothing about what it is. Name what it holds or does. See docs/backend/code-style.md.",
		category: "Naming",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	private static readonly ImmutableArray<string> Suffixes =
		["Helper", "Helpers", "Util", "Utils", "Utility", "Utilities", "Info", "Data", "Shape"];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [VagueSuffix];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSymbolAction(Analyze, SymbolKind.NamedType, SymbolKind.Method, SymbolKind.Property);
	}

	private static void Analyze(SymbolAnalysisContext ctx)
	{
		var symbol = ctx.Symbol;
		if (symbol.IsImplicitlyDeclared || symbol.Locations.FirstOrDefault() is not { IsInSource: true } location)
			return;
		if (symbol is IMethodSymbol { MethodKind: not MethodKind.Ordinary })
			return;

		var suffix = Suffixes.FirstOrDefault(s => symbol.Name.Length > s.Length && symbol.Name.EndsWith(s, System.StringComparison.Ordinal));
		if (suffix is null || NameComesFromElsewhere(symbol))
			return;

		ctx.ReportDiagnostic(Diagnostic.Create(VagueSuffix, location, symbol.Name, suffix));
	}

	/// <summary>An override or an implementation of an interface member takes its name from the base.</summary>
	private static bool NameComesFromElsewhere(ISymbol symbol)
	{
		if (symbol.IsOverride || symbol is INamedTypeSymbol)
			return symbol.IsOverride;
		if (symbol is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 } or IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 })
			return true;

		var type = symbol.ContainingType;
		return type.AllInterfaces
			.SelectMany(i => i.GetMembers(symbol.Name))
			.Any(member => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), symbol));
	}
}
