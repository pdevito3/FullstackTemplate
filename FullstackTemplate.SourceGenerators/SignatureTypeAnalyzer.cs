namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

/// <summary>
/// Refuses a signature that hides its type: a parameter or a return of object or dynamic in app code, and a bag of
/// untyped values (a dictionary of object, an ExpandoObject, or a JSON DOM type) in a signature of the domain. See
/// "Signatures" in docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SignatureTypeAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor ObjectSignature = new(
		id: "FSTTYP004",
		title: "A signature takes or returns object",
		messageFormat: "{0} is {1}, so each caller and the method must test the type. Use the type that it holds, or suppress FSTTYP004 with the reason at a boundary that gets values of any type. See docs/backend/code-style.md.",
		category: "Types",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AppCode]);

	internal static readonly DiagnosticDescriptor UntypedBagInDomain = new(
		id: "FSTTYP005",
		title: "A signature of the domain holds untyped values",
		messageFormat: "{0} is {1}, which holds values of no fixed type. Parse it into a record at the boundary and pass the record to the domain. See docs/backend/code-style.md.",
		category: "Types",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private static readonly ImmutableHashSet<string> DomTypes =
	[
		"System.Dynamic.ExpandoObject", "System.Text.Json.JsonElement", "System.Text.Json.JsonDocument",
		"System.Text.Json.Nodes.JsonNode", "System.Text.Json.Nodes.JsonObject", "Newtonsoft.Json.Linq.JObject",
	];

	private static readonly ImmutableHashSet<string> Dictionaries =
	[
		"System.Collections.Generic.Dictionary<TKey, TValue>", "System.Collections.Generic.IDictionary<TKey, TValue>",
		"System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>",
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ObjectSignature, UntypedBagInDomain];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterSymbolAction(Analyze, SymbolKind.Method, SymbolKind.Property, SymbolKind.Field);
	}

	private static void Analyze(SymbolAnalysisContext ctx)
	{
		var symbol = ctx.Symbol;
		if (symbol.IsImplicitlyDeclared || symbol.Locations.FirstOrDefault() is not { IsInSource: true } location || TakesShapeFromBase(symbol))
			return;

		var inDomain = IsDomain(symbol.ContainingNamespace);
		switch (symbol)
		{
			case IMethodSymbol method when method.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.DelegateInvoke:
				foreach (var parameter in method.Parameters)
				{
					var at = parameter.Locations.FirstOrDefault() ?? location;
					if (IsObject(parameter.Type))
						ctx.ReportDiagnostic(Diagnostic.Create(ObjectSignature, at, $"The parameter '{parameter.Name}' of {Name(method)}", parameter.Type.ToDisplayString()));
					if (inDomain && IsBag(parameter.Type))
						ctx.ReportDiagnostic(Diagnostic.Create(UntypedBagInDomain, at, $"The parameter '{parameter.Name}' of {Name(method)}", parameter.Type.ToDisplayString()));
				}

				if (method.MethodKind == MethodKind.Constructor || method.ReturnsVoid)
					break;
				if (IsObject(Awaited(method.ReturnType)))
					ctx.ReportDiagnostic(Diagnostic.Create(ObjectSignature, location, $"The return of {Name(method)}", method.ReturnType.ToDisplayString()));
				if (inDomain && IsBag(method.ReturnType))
					ctx.ReportDiagnostic(Diagnostic.Create(UntypedBagInDomain, location, $"The return of {Name(method)}", method.ReturnType.ToDisplayString()));
				break;

			case IPropertySymbol property when inDomain && IsBag(property.Type):
				ctx.ReportDiagnostic(Diagnostic.Create(UntypedBagInDomain, location, $"The property '{property.Name}'", property.Type.ToDisplayString()));
				break;

			case IFieldSymbol field when inDomain && IsBag(field.Type):
				ctx.ReportDiagnostic(Diagnostic.Create(UntypedBagInDomain, location, $"The field '{field.Name}'", field.Type.ToDisplayString()));
				break;
		}
	}

	private static string Name(IMethodSymbol method) =>
		method.MethodKind == MethodKind.Constructor ? $"the constructor of {method.ContainingType.Name}" : method.Name;

	private static bool IsObject(ITypeSymbol type) =>
		type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;

	/// <summary>The T of a Task&lt;T&gt; or a ValueTask&lt;T&gt;, else the type.</summary>
	private static ITypeSymbol Awaited(ITypeSymbol type) =>
		type is INamedTypeSymbol { IsGenericType: true, Name: "Task" or "ValueTask", TypeArguments.Length: 1 } task
		&& task.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"
			? task.TypeArguments[0]
			: type;

	/// <summary>A dictionary of string to object, a JSON DOM type, or an ExpandoObject, also inside a generic or an array.</summary>
	private static bool IsBag(ITypeSymbol type) => type switch
	{
		IArrayTypeSymbol array => IsBag(array.ElementType),
		INamedTypeSymbol named when DomTypes.Contains(named.ToDisplayString()) => true,
		INamedTypeSymbol { IsGenericType: true } generic when Dictionaries.Contains(generic.OriginalDefinition.ToDisplayString())
			&& generic.TypeArguments[0].SpecialType == SpecialType.System_String
			&& IsObject(generic.TypeArguments[1]) => true,
		INamedTypeSymbol { IsGenericType: true } generic => generic.TypeArguments.Any(IsBag),
		_ => false,
	};

	private static bool IsDomain(INamespaceSymbol? ns)
	{
		for (; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
		{
			if (ns.Name == "Domain")
				return true;
		}

		return false;
	}

	/// <summary>An override or an implementation of an interface member takes its signature from the base.</summary>
	private static bool TakesShapeFromBase(ISymbol symbol)
	{
		if (symbol.IsOverride)
			return true;
		if (symbol is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 } or IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 })
			return true;

		var type = symbol.ContainingType;
		return type is not null && type.AllInterfaces
			.SelectMany(i => i.GetMembers(symbol.Name))
			.Any(member => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member), symbol));
	}
}
