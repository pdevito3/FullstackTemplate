namespace FullstackTemplate.SourceGenerators;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Refuses a <c>new</c> of a type that the container builds: a type argument of an AddScoped, AddSingleton,
/// AddTransient, AddHostedService, or AddDbContext call (or its TryAdd and keyed forms) with no factory and no
/// instance, or a typeof argument of one. A new copy skips the lifetime, the decorators, and the options of the
/// registration. A design-time DbContext factory may build its context. See "Services" in docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceConstructionAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor NewOfService = new(
		id: "FSTDI001",
		title: "Code builds a type that the container builds",
		messageFormat: "'{0}' is registered for the container to build, so this new skips its lifetime and its decorators. Inject it. See docs/backend/code-style.md.",
		category: "Services",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AppCode, WellKnownDiagnosticTags.CompilationEnd]);

	private static readonly ImmutableHashSet<string> Registrations =
	[
		"AddScoped", "AddSingleton", "AddTransient", "TryAddScoped", "TryAddSingleton", "TryAddTransient",
		"AddKeyedScoped", "AddKeyedSingleton", "AddKeyedTransient", "TryAddKeyedScoped", "TryAddKeyedSingleton",
		"TryAddKeyedTransient", "AddHostedService", "AddDbContext",
	];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [NewOfService];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterCompilationStartAction(start =>
		{
			var designTimeFactory = start.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory`1");
			var built = new ConcurrentDictionary<ITypeSymbol, byte>(SymbolEqualityComparer.Default);
			var creations = new ConcurrentBag<IObjectCreationOperation>();

			start.RegisterOperationAction(ctx =>
			{
				var call = (IInvocationOperation)ctx.Operation;
				if (!IsRegistration(call.TargetMethod))
					return;

				var values = call.Arguments
					.Where(a => a.ArgumentKind != ArgumentKind.DefaultValue && !(call.TargetMethod.IsExtensionMethod && a.Parameter?.Ordinal == 0))
					.Where(a => a.Parameter?.Name != "serviceKey")
					.Select(a => Unwrap(a.Value))
					.ToList();
				// A factory or an instance builds the value in code. The lambda of AddDbContext sets options, and the
				// container still builds the context.
				if (call.TargetMethod.Name == "AddDbContext" || values.All(v => v is ITypeOfOperation))
				{
					foreach (var type in call.TargetMethod.TypeArguments.Concat(values.OfType<ITypeOfOperation>().Select(t => t.TypeOperand)))
						built.TryAdd(type, 0);
				}
			}, OperationKind.Invocation);

			start.RegisterOperationAction(ctx =>
			{
				var creation = (IObjectCreationOperation)ctx.Operation;
				if (creation.Type is { TypeKind: TypeKind.Class })
					creations.Add(creation);
			}, OperationKind.ObjectCreation);

			start.RegisterCompilationEndAction(end =>
			{
				foreach (var creation in creations)
				{
					if (!built.ContainsKey(creation.Type!) || InDesignTimeFactory(creation, designTimeFactory))
						continue;
					end.ReportDiagnostic(Diagnostic.Create(NewOfService, creation.Syntax.GetLocation(), creation.Type!.ToDisplayString()));
				}
			});
		});
	}

	private static bool IsRegistration(IMethodSymbol method) =>
		Registrations.Contains(method.Name)
		&& method.ContainingNamespace?.ToDisplayString() is "Microsoft.Extensions.DependencyInjection" or "Microsoft.Extensions.DependencyInjection.Extensions";

	private static bool InDesignTimeFactory(IObjectCreationOperation creation, INamedTypeSymbol? designTimeFactory)
	{
		if (designTimeFactory is null)
			return false;

		var model = creation.SemanticModel;
		var type = model?.GetEnclosingSymbol(creation.Syntax.SpanStart)?.ContainingType;
		return type is not null && type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, designTimeFactory));
	}

	/// <summary>The operation under its implicit conversions.</summary>
	private static IOperation Unwrap(IOperation operation)
	{
		while (operation is IConversionOperation { IsImplicit: true } conversion)
			operation = conversion.Operand;
		return operation;
	}
}
