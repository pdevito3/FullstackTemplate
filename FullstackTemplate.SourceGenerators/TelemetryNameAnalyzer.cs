namespace FullstackTemplate.SourceGenerators;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TelemetryNameAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor BadName = new(
		id: "FSTTEL004",
		title: "A telemetry name breaks the name rules",
		messageFormat: "The {0} '{1}' breaks the name rules. Use {2}, for example {3}. See the Names section of docs/backend/feature-telemetry.md.",
		category: "Telemetry",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	private const string Part = "[a-z][a-z0-9]*(?:_[a-z0-9]+)*";

	private const string DottedForm = "lower case parts in snake_case joined by dots, as area.operation";

	private static readonly Regex Dotted = new($@"^{Part}(?:\.{Part})+$", RegexOptions.CultureInvariant);

	private static readonly NameRule SpanName = new("span name", Dotted, DottedForm, "invoice.render");

	private static readonly NameRule EventName = new("event name", Dotted, DottedForm, "migration.lock_acquired");

	private static readonly NameRule QueryName = new("query name", Dotted, DottedForm, "users.get_by_id");

	internal static readonly ImmutableArray<string> SemanticConventionNamespaces =
	[
		"aspnetcore", "aws", "client", "cloud", "code", "container", "cpu", "db", "deployment", "dns", "dotnet",
		"error", "exception", "faas", "gen_ai", "grpc", "host", "http", "jsonrpc", "k8s", "kestrel", "mcp",
		"messaging", "network", "otel", "peer", "process", "rpc", "server", "service", "telemetry", "thread",
		"tls", "url", "user_agent",
	];

	private static readonly NameRule TagKey = new(
		"tag key",
		new Regex($@"^(?:app|{string.Join("|", SemanticConventionNamespaces)})(?:\.{Part})+$", RegexOptions.CultureInvariant),
		"app. and then lower case parts in snake_case joined by dots, as app.[entity]_id or app.[area].[name]",
		"app.user_id or app.user.role");

	private static readonly NameRule MetricName = new(
		"metric name",
		new Regex($@"^app\.{Part}\.{Part}$", RegexOptions.CultureInvariant),
		"app.[entities].[name] in lower case parts in snake_case",
		"app.users.role_changed");

	private static readonly ImmutableHashSet<string> PlatformMetrics = ["app.errors", "app.request.duration"];

	private static readonly NameRule LogProperty = new(
		"log property",
		new Regex("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant),
		"a PascalCase name",
		"{UserId}");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [BadName];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			var types = new NameTypes(start.Compilation);
			start.RegisterOperationAction(ctx => AnalyzeInvocation(ctx, types), OperationKind.Invocation);
			start.RegisterOperationAction(ctx => AnalyzeCreation(ctx, types), OperationKind.ObjectCreation);
			start.RegisterOperationAction(ctx => AnalyzeCollectionExpression(ctx, types), OperationKind.CollectionExpression);
			start.RegisterOperationAction(ctx => AnalyzeAssignment(ctx, types), OperationKind.SimpleAssignment, OperationKind.CompoundAssignment, OperationKind.CoalesceAssignment);
		});
	}

	private static void AnalyzeInvocation(OperationAnalysisContext ctx, NameTypes types)
	{
		var call = (IInvocationOperation)ctx.Operation;
		var method = call.TargetMethod;
		var owner = method.ContainingType;

		if (types.IsTelemetry(owner))
		{
			Check(ctx, Argument(call.Arguments, "spanName"), SpanName);
			Check(ctx, Argument(call.Arguments, "traceName"), SpanName);
			Check(ctx, Argument(call.Arguments, "eventName"), EventName);
			if (Argument(call.Arguments, "tags") is { } tags)
			{
				foreach (var tuple in TagTuples(tags))
					Check(ctx, tuple.Elements[0], TagKey);
			}
		}
		else if (types.IsQueryTagging(owner) && method.Name == "TagWithOperationCallSite")
			Check(ctx, Argument(call.Arguments, "operationName"), QueryName);
		else if (types.IsActivity(owner) && method.Name == "SetTag")
			Check(ctx, Argument(call.Arguments, "key"), TagKey);
		else if (types.IsTagCollection(owner) && method.Name is "Add" or "Insert")
			CheckAddedTag(ctx, call.Arguments, types);
		else if (types.IsMeter(owner) && method.Name.StartsWith("Create", System.StringComparison.Ordinal))
			Check(ctx, Argument(call.Arguments, "name"), MetricName, PlatformMetrics);
		else if (types.TemplateParameter(method) is { } parameter
			&& Argument(call.Arguments, parameter) is { ConstantValue: { HasValue: true, Value: string template } } value)
		{
			foreach (var name in PropertyNames(template))
			{
				if (!LogProperty.Pattern.IsMatch(name))
					Report(ctx, value, LogProperty, name);
			}
		}
	}

	private static void AnalyzeCreation(OperationAnalysisContext ctx, NameTypes types)
	{
		var creation = (IObjectCreationOperation)ctx.Operation;
		if (types.IsActivityEvent(creation.Constructor?.ContainingType))
			Check(ctx, Argument(creation.Arguments, "name"), EventName);
		else if (types.IsTagCollection(creation.Constructor?.ContainingType))
		{
			foreach (var argument in creation.Arguments)
				CheckTagPairs(ctx, argument.Value, types);
		}
	}

	private static void AnalyzeCollectionExpression(OperationAnalysisContext ctx, NameTypes types)
	{
		var collection = (ICollectionExpressionOperation)ctx.Operation;
		if (types.IsTagCollection(collection.Type as INamedTypeSymbol))
			CheckTagPairs(ctx, collection, types);
	}

	private static void AnalyzeAssignment(OperationAnalysisContext ctx, NameTypes types)
	{
		var assignment = (IAssignmentOperation)ctx.Operation;
		if (assignment.Target is IPropertyReferenceOperation { Property.IsIndexer: true } indexer && types.IsTagCollection(indexer.Property.ContainingType))
		{
			Check(ctx, Argument(indexer.Arguments, "key"), TagKey);
			if (types.IsTagPair(indexer.Property.Type))
				CheckTagPair(ctx, assignment.Value, types);
		}
	}

	/// <summary>
	/// The names of the properties of a Serilog message template. A token that Serilog reads as text, such as
	/// "{{" or "{ a }", is not a property. "@" and "$" before a name, and an alignment or a format after it, are legal.
	/// </summary>
	internal static IEnumerable<string> PropertyNames(string template)
	{
		for (var i = 0; i < template.Length; i++)
		{
			if (template[i] != '{')
				continue;
			if (i + 1 < template.Length && template[i + 1] == '{')
			{
				i++;
				continue;
			}

			var end = template.IndexOf('}', i + 1);
			if (end < 0)
				yield break;

			var start = i + 1 < end && template[i + 1] is '@' or '$' ? i + 2 : i + 1;
			var stop = start;
			while (stop < end && template[stop] is not (',' or ':'))
				stop++;

			var name = template.Substring(start, stop - start);
			if (name.Length > 0 && name.All(static c => char.IsLetterOrDigit(c) || c is '_' or '.'))
				yield return name;
			i = end;
		}
	}

	private static IEnumerable<ITupleOperation> TagTuples(IOperation tags)
		=> ValuesHeldBy(Unwrap(tags))
			.SelectMany(ItemsOf)
			.Select(Unwrap)
			.OfType<ITupleOperation>()
			.Where(static tuple => tuple.Elements.Length == 2);

	private static IEnumerable<IOperation> ValuesHeldBy(IOperation value)
	{
		if (value is not ILocalReferenceOperation { Local: var local })
			return [value];

		var root = value;
		while (root.Parent is not null)
			root = root.Parent;

		return root.Descendants().SelectMany<IOperation, IOperation>(operation => operation switch
		{
			IVariableDeclaratorOperation { Initializer: { } initializer } declarator when SymbolEqualityComparer.Default.Equals(declarator.Symbol, local) => [initializer.Value],
			ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment when SymbolEqualityComparer.Default.Equals(target.Local, local) => [assignment.Value],
			_ => [],
		});
	}

	private static IEnumerable<IOperation> ItemsOf(IOperation collection)
		=> Unwrap(collection) switch
		{
			ICollectionExpressionOperation expression => expression.Elements,
			IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
			_ => [],
		};

	private static void CheckAddedTag(OperationAnalysisContext ctx, ImmutableArray<IArgumentOperation> arguments, NameTypes types)
	{
		Check(ctx, Argument(arguments, "key"), TagKey);
		foreach (var argument in arguments.Where(a => types.IsTagPair(a.Parameter?.Type)))
			CheckTagPair(ctx, argument.Value, types);
	}

	private static void CheckTagPairs(OperationAnalysisContext ctx, IOperation source, NameTypes types)
	{
		foreach (var held in ValuesHeldBy(Unwrap(source)))
		{
			foreach (var item in ItemsOf(held))
				CheckTagPair(ctx, item, types);

			if (Unwrap(held) is IObjectCreationOperation { Initializer: { } initializer })
				CheckInitializer(ctx, initializer, types);
		}
	}

	private static void CheckInitializer(OperationAnalysisContext ctx, IObjectOrCollectionInitializerOperation initializer, NameTypes types)
	{
		foreach (var entry in initializer.Initializers)
		{
			if (entry is IInvocationOperation add && types.IsListOrDictionary(add.TargetMethod.ContainingType))
				CheckAddedTag(ctx, add.Arguments, types);
			else if (entry is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.IsIndexer: true } indexer } && types.IsListOrDictionary(indexer.Property.ContainingType))
				Check(ctx, Argument(indexer.Arguments, "key"), TagKey);
		}
	}

	private static void CheckTagPair(OperationAnalysisContext ctx, IOperation pair, NameTypes types)
	{
		if (Unwrap(pair) is IObjectCreationOperation creation && types.IsTagPair(creation.Type))
			Check(ctx, Argument(creation.Arguments, "key"), TagKey);
	}

	private static IOperation Unwrap(IOperation operation)
	{
		while (operation is IConversionOperation conversion)
			operation = conversion.Operand;
		return operation;
	}

	private static IOperation? Argument(ImmutableArray<IArgumentOperation> arguments, string parameter)
		=> arguments.FirstOrDefault(a => a.Parameter?.Name == parameter)?.Value;

	private static void Check(OperationAnalysisContext ctx, IOperation? value, NameRule rule, ImmutableHashSet<string>? allowed = null)
	{
		if (value?.ConstantValue is { HasValue: true, Value: string name } && !rule.Pattern.IsMatch(name) && allowed?.Contains(name) != true)
			Report(ctx, value, rule, name);
	}

	private static void Report(OperationAnalysisContext ctx, IOperation value, NameRule rule, string name)
		=> ctx.ReportDiagnostic(Diagnostic.Create(BadName, value.Syntax.GetLocation(), rule.Kind, name, rule.Form, rule.Example));

	private sealed class NameRule(string kind, Regex pattern, string form, string example)
	{
		public string Kind { get; } = kind;
		public Regex Pattern { get; } = pattern;
		public string Form { get; } = form;
		public string Example { get; } = example;
	}

	private sealed class NameTypes(Compilation compilation)
	{
		private readonly INamedTypeSymbol? telemetry = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Resources.Telemetry");
		private readonly INamedTypeSymbol? queryTagging = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Databases.QueryTagging");
		private readonly INamedTypeSymbol? activity = compilation.GetTypeByMetadataName("System.Diagnostics.Activity");
		private readonly INamedTypeSymbol? activityEvent = compilation.GetTypeByMetadataName("System.Diagnostics.ActivityEvent");
		private readonly INamedTypeSymbol? meter = compilation.GetTypeByMetadataName("System.Diagnostics.Metrics.Meter");
		private readonly INamedTypeSymbol? tagList = compilation.GetTypeByMetadataName("System.Diagnostics.TagList");
		private readonly INamedTypeSymbol? activityTagsCollection = compilation.GetTypeByMetadataName("System.Diagnostics.ActivityTagsCollection");
		private readonly INamedTypeSymbol? list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
		private readonly INamedTypeSymbol? dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
		private readonly INamedTypeSymbol? keyValuePair = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
		private readonly INamedTypeSymbol? templateFormatMethod = compilation.GetTypeByMetadataName("Serilog.Core.MessageTemplateFormatMethodAttribute");

		public bool IsTelemetry(INamedTypeSymbol? type) => Is(type, telemetry);
		public bool IsQueryTagging(INamedTypeSymbol? type) => Is(type, queryTagging);
		public bool IsActivity(INamedTypeSymbol? type) => Is(type, activity);
		public bool IsActivityEvent(INamedTypeSymbol? type) => Is(type, activityEvent);
		public bool IsMeter(INamedTypeSymbol? type) => Is(type, meter);
		public bool IsTagCollection(INamedTypeSymbol? type) => Is(type, tagList) || Is(type, activityTagsCollection);

		public bool IsListOrDictionary(INamedTypeSymbol type) => Is(type.OriginalDefinition, list) || Is(type.OriginalDefinition, dictionary);

		public bool IsTagPair(ITypeSymbol? type)
			=> type is INamedTypeSymbol pair
				&& Is(pair.OriginalDefinition, keyValuePair)
				&& pair.TypeArguments[0].SpecialType == SpecialType.System_String
				&& pair.TypeArguments[1].SpecialType == SpecialType.System_Object;

		public string? TemplateParameter(IMethodSymbol method)
			=> templateFormatMethod is null
				? null
				: method.GetAttributes()
					.FirstOrDefault(a => Is(a.AttributeClass, templateFormatMethod))?
					.ConstructorArguments.FirstOrDefault().Value as string;

		private static bool Is(INamedTypeSymbol? type, INamedTypeSymbol? expected)
			=> expected is not null && SymbolEqualityComparer.Default.Equals(type, expected);
	}
}
