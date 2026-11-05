namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SaveTelemetryAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor SaveWithoutTelemetry = new(
		id: "FSTTEL002",
		title: "A command handler saves with no state change log or no ID tag",
		messageFormat: "The handler '{0}' saves changes with no {1}. Add {2}. See docs/backend/feature-telemetry.md.",
		category: "Telemetry",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string LogFix = "a _logger.Here().Information log with a message template after the save, "
		+ """for example _logger.Here().Information("Updated order {OrderId}", order.Id)""";

	private const string TagFix = """an Activity.Current?.SetTag("app.<entity>_id", ...) tag as soon as the handler knows the ID, """
		+ """for example Activity.Current?.SetTag("app.order_id", order.Id)""";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [SaveWithoutTelemetry];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (HandlerTypes.From(start.Compilation) is { } types)
				start.RegisterSymbolStartAction(handler => AnalyzeHandler(handler, types), SymbolKind.NamedType);
		});
	}

	private static void AnalyzeHandler(SymbolStartAnalysisContext start, HandlerTypes types)
	{
		var handler = (INamedTypeSymbol)start.Symbol;
		if (!types.IsRequestHandler(handler))
			return;

		var found = new HandlerCalls();
		start.RegisterOperationAction(ctx =>
		{
			var call = (IInvocationOperation)ctx.Operation;
			if (types.IsSave(call.TargetMethod))
				found.Saves = true;
			else if (types.IsStateChangeLog(call))
				found.Logs = true;
			else if (types.IsIdTag(call))
				found.Tags = true;
		}, OperationKind.Invocation);

		start.RegisterSymbolEndAction(end =>
		{
			if (!found.Saves || (found.Logs && found.Tags))
				return;

			var (missing, fix) = (found.Logs, found.Tags) switch
			{
				(false, false) => ("state change log and no ID tag", LogFix + ", and " + TagFix),
				(false, _) => ("state change log", LogFix),
				_ => ("ID tag", TagFix),
			};
			end.ReportDiagnostic(Diagnostic.Create(
				SaveWithoutTelemetry, handler.Locations.FirstOrDefault() ?? Location.None, handler.ToDisplayString(), missing, fix));
		});
	}

	private sealed class HandlerCalls
	{
		public volatile bool Saves;
		public volatile bool Logs;
		public volatile bool Tags;
	}

	private sealed class HandlerTypes(
		INamedTypeSymbol dbContext,
		INamedTypeSymbol activity,
		INamedTypeSymbol logger,
		INamedTypeSymbol loggerExtensions,
		ImmutableArray<INamedTypeSymbol> requestHandlers)
	{
		public static HandlerTypes? From(Compilation compilation)
		{
			var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
			var activity = compilation.GetTypeByMetadataName("System.Diagnostics.Activity");
			var logger = compilation.GetTypeByMetadataName("Serilog.ILogger");
			var loggerExtensions = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Resources.LoggerExtensions");
			var requestHandlers = new[] { "MediatR.IRequestHandler`1", "MediatR.IRequestHandler`2" }
				.Select(compilation.GetTypeByMetadataName)
				.OfType<INamedTypeSymbol>()
				.ToImmutableArray();

			return dbContext is null || activity is null || logger is null || loggerExtensions is null || requestHandlers.IsEmpty
				? null
				: new HandlerTypes(dbContext, activity, logger, loggerExtensions, requestHandlers);
		}

		public bool IsRequestHandler(INamedTypeSymbol type)
			=> type.TypeKind == TypeKind.Class
				&& type.AllInterfaces.Any(i => requestHandlers.Contains(i.OriginalDefinition, SymbolEqualityComparer.Default));

		public bool IsSave(IMethodSymbol method)
		{
			if (method.Name is not ("SaveChangesAsync" or "SaveChanges"))
				return false;

			for (var current = method.ContainingType; current is not null; current = current.BaseType)
			{
				if (SymbolEqualityComparer.Default.Equals(current, dbContext))
					return true;
			}

			return false;
		}

		public bool IsStateChangeLog(IInvocationOperation call)
			=> call.TargetMethod is { Name: "Information" } method
				&& SymbolEqualityComparer.Default.Equals(method.ContainingType, logger)
				&& call.Instance is IInvocationOperation { TargetMethod: { Name: "Here" } here }
				&& SymbolEqualityComparer.Default.Equals(here.ContainingType, loggerExtensions)
				&& call.Arguments.Any(static a => a.Parameter?.Name == "messageTemplate" && a.Value.ConstantValue.HasValue);

		public bool IsIdTag(IInvocationOperation call)
		{
			if (call.TargetMethod.Name != "SetTag"
				|| !SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, activity)
				|| call.Arguments.Length == 0
				|| call.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: string key }
				|| !key.StartsWith("app.", System.StringComparison.Ordinal)
				|| !key.EndsWith("_id", System.StringComparison.Ordinal))
				return false;

			var instance = call.Instance;
			if (instance is IConditionalAccessInstanceOperation)
			{
				for (var parent = call.Parent; parent is not null; parent = parent.Parent)
				{
					if (parent is IConditionalAccessOperation access)
					{
						instance = access.Operation;
						break;
					}
				}
			}

			if (instance is ILocalReferenceOperation local)
				instance = InitializerIfNeverReassigned(local);

			return instance is IPropertyReferenceOperation { Property.Name: "Current" } current
				&& SymbolEqualityComparer.Default.Equals(current.Property.ContainingType, activity);
		}

		private static IOperation? InitializerIfNeverReassigned(ILocalReferenceOperation reference)
		{
			var root = (IOperation)reference;
			while (root.Parent is not null)
				root = root.Parent;

			var local = reference.Local;
			var assigned = root.Descendants().Any(o => o is ISimpleAssignmentOperation { Target: ILocalReferenceOperation target }
				&& SymbolEqualityComparer.Default.Equals(target.Local, local));
			return assigned
				? null
				: root.Descendants()
					.OfType<IVariableDeclaratorOperation>()
					.FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Symbol, local))?
					.Initializer?.Value;
		}
	}
}
