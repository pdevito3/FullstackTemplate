namespace FullstackTemplate.SourceGenerators;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryInputAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor RawRequestText = new(
		id: "FSTQRY001",
		title: "A query compares a text of the request",
		messageFormat: "The query compares the request value '{0}', which can hold a NUL that Postgres cannot take. Compare the entity value, or check the input with EntityId.IsValid or PostgresText.CanStore first. For a list, check each item. See docs/backend/features-and-cqrs.md.",
		category: "QueryInput",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [RawRequestText];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			if (InputTypes.From(start.Compilation) is not { } types)
				return;

			start.RegisterOperationAction(ctx => AnalyzeCompare(ctx, types), OperationKind.Binary, OperationKind.Invocation);
		});
	}

	private static readonly ImmutableHashSet<string> NulKeepingCalls =
		["Trim", "TrimStart", "TrimEnd", "ToLower", "ToUpper", "ToLowerInvariant", "ToUpperInvariant"];

	private static void AnalyzeCompare(OperationAnalysisContext ctx, InputTypes types)
	{
		IEnumerable<IOperation> operands = ctx.Operation switch
		{
			IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals } binary
				when !IsNull(binary.LeftOperand) && !IsNull(binary.RightOperand) => [binary.LeftOperand, binary.RightOperand],
			IInvocationOperation call when types.SendsArguments(call.TargetMethod)
				=> [.. call.Arguments.Select(static a => a.Value), .. call.Instance is null ? [] : new[] { call.Instance }],
			_ => [],
		};

		foreach (var operand in operands)
		{
			if (PathFromFirstParameter(operand, types) is not { } path || !types.IsHandle(path[0].ContainingSymbol) || !types.InQueryLambda(ctx.Operation))
				continue;

			if (!IsCheckedBefore(ctx.Operation, path, types))
				ctx.ReportDiagnostic(Diagnostic.Create(RawRequestText, operand.Syntax.GetLocation(), operand.Syntax.ToString()));
		}
	}

	// EF evaluates a compare of a request value with null on the client, so the text does not go to Postgres
	private static bool IsNull(IOperation operand) => operand.ConstantValue is { HasValue: true, Value: null };

	private static List<ISymbol>? PathFromFirstParameter(IOperation operand, InputTypes types)
	{
		var value = Unwrap(operand);
		if (value.Type?.SpecialType != SpecialType.System_String && !types.IsTextList(value.Type))
			return null;

		var path = new List<ISymbol>();
		while (true)
		{
			switch (value)
			{
				case IPropertyReferenceOperation { Instance: { } instance } property:
					path.Add(property.Property);
					value = instance;
					break;
				case IFieldReferenceOperation { Instance: { } instance } field:
					path.Add(field.Field);
					value = instance;
					break;
				case IParameterReferenceOperation { Parameter.Ordinal: 0 } parameter:
					path.Add(parameter.Parameter);
					path.Reverse();
					return path;
				default:
					return null;
			}
		}
	}

	private static IOperation Unwrap(IOperation operand)
	{
		var value = operand;
		while (true)
		{
			switch (value)
			{
				case IConversionOperation { IsImplicit: true } conversion:
					value = conversion.Operand;
					break;
				case IInvocationOperation { Instance: { } instance, TargetMethod: { ContainingType.SpecialType: SpecialType.System_String, Parameters.Length: 0 } } call
					when NulKeepingCalls.Contains(call.TargetMethod.Name):
					value = instance;
					break;
				default:
					return value;
			}
		}
	}

	private static bool IsCheckedBefore(IOperation compare, List<ISymbol> path, InputTypes types)
	{
		var root = compare;
		while (root.Parent is not null)
			root = root.Parent;

		return root.Descendants()
			.Where(check => check.Syntax.SpanStart < compare.Syntax.SpanStart)
			.SelectMany(check => CheckedValues(check, root, types))
			.Any(value => PathFromFirstParameter(value, types) is { } checkedPath && checkedPath.SequenceEqual(path, SymbolEqualityComparer.Default));
	}

	private static IEnumerable<IOperation> CheckedValues(IOperation operation, IOperation root, InputTypes types)
	{
		switch (operation)
		{
			case IInvocationOperation call when types.IsCheck(call.TargetMethod):
				foreach (var argument in call.Arguments)
				{
					yield return argument.Value;
					if (ListThatHoldsItem(argument.Value, root) is { } list)
						yield return list;
				}

				break;
			case IMethodReferenceOperation reference when types.IsCheck(reference.Method) && SourceOf(reference.Parent) is { } source:
				yield return source;
				break;
		}
	}

	private static IOperation? ListThatHoldsItem(IOperation value, IOperation root)
	{
		ISymbol item;
		switch (Unwrap(value))
		{
			case IArrayElementReferenceOperation { Indices.Length: 1 } element when InForLoopOf(element, element.Indices[0]):
				return element.ArrayReference;
			case IPropertyReferenceOperation { Property.IsIndexer: true, Instance: { } list, Arguments.Length: 1 } element
				when InForLoopOf(element, element.Arguments[0].Value):
				return list;
			case ILocalReferenceOperation local when InitializerIfNeverReassigned(local.Local, root) is { } initializer:
				return ListThatHoldsItem(initializer, root);
			case IParameterReferenceOperation parameter:
				item = parameter.Parameter;
				break;
			case ILocalReferenceOperation local:
				item = local.Local;
				break;
			default:
				return null;
		}

		for (var parent = value.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IAnonymousFunctionOperation function && function.Symbol.Parameters.Contains(item, SymbolEqualityComparer.Default))
				return SourceOf(function.Parent);
			if (parent is IForEachLoopOperation loop && loop.Locals.Contains(item, SymbolEqualityComparer.Default))
				return ListOf(loop.Collection);
		}

		return null;
	}

	private static bool InForLoopOf(IOperation operation, IOperation index)
	{
		if (Unwrap(index) is not ILocalReferenceOperation { Local: var local })
			return false;

		for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IForLoopOperation loop && loop.Locals.Contains(local, SymbolEqualityComparer.Default))
				return true;
		}

		return false;
	}

	private static IOperation? InitializerIfNeverReassigned(ILocalSymbol local, IOperation root)
	{
		var descendants = root.Descendants().ToList();
		return descendants.OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, local) && IsWrite(r))
			? null
			: descendants
				.OfType<IVariableDeclaratorOperation>()
				.FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Symbol, local))
				?.Initializer?.Value;
	}

	private static bool IsWrite(IOperation reference)
	{
		var target = reference;
		while (target.Parent is ITupleOperation tuple)
			target = tuple;

		return target.Parent switch
		{
			IAssignmentOperation assignment => ReferenceEquals(assignment.Target, target),
			IIncrementOrDecrementOperation => true,
			IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out } => true,
			_ => false,
		};
	}

	private static IOperation? SourceOf(IOperation? @delegate)
		=> @delegate is IDelegateCreationOperation { Parent: IArgumentOperation { Parent: IInvocationOperation call } argument }
			&& (call.Instance ?? (call.TargetMethod.IsExtensionMethod && !ReferenceEquals(call.Arguments[0], argument) ? call.Arguments[0].Value : null)) is { } source
				? ListOf(source)
				: null;

	private static IOperation ListOf(IOperation collection)
	{
		var list = Unwrap(collection);
		if (list is IConditionalAccessInstanceOperation)
		{
			for (var parent = list.Parent; parent is not null; parent = parent.Parent)
			{
				if (parent is IConditionalAccessOperation access)
				{
					list = Unwrap(access.Operation);
					break;
				}
			}
		}

		return list is ICoalesceOperation coalesce ? coalesce.Value : list;
	}

	private sealed class InputTypes(
		INamedTypeSymbol entityId,
		INamedTypeSymbol postgresText,
		INamedTypeSymbol dbFunctions,
		INamedTypeSymbol genericEnumerable,
		INamedTypeSymbol queryable,
		INamedTypeSymbol lambdaExpression,
		ImmutableArray<INamedTypeSymbol> requestHandlers)
	{
		public static InputTypes? From(Compilation compilation)
		{
			var entityId = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Domain.EntityId");
			var postgresText = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Databases.PostgresText");
			var dbFunctions = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbFunctions");
			var genericEnumerable = compilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
			var queryable = compilation.GetTypeByMetadataName("System.Linq.IQueryable");
			var lambdaExpression = compilation.GetTypeByMetadataName("System.Linq.Expressions.LambdaExpression");
			var requestHandlers = new[] { "MediatR.IRequestHandler`1", "MediatR.IRequestHandler`2" }
				.Select(compilation.GetTypeByMetadataName)
				.OfType<INamedTypeSymbol>()
				.ToImmutableArray();

			return entityId is null || postgresText is null || dbFunctions is null || queryable is null || lambdaExpression is null || requestHandlers.IsEmpty
				? null
				: new InputTypes(entityId, postgresText, dbFunctions, genericEnumerable, queryable, lambdaExpression, requestHandlers);
		}

		public bool IsCheck(IMethodSymbol method)
			=> (method.Name == "IsValid" && SymbolEqualityComparer.Default.Equals(method.ContainingType, entityId))
				|| (method.Name == "CanStore" && SymbolEqualityComparer.Default.Equals(method.ContainingType, postgresText));

		public bool SendsArguments(IMethodSymbol method)
			=> method.Name switch
			{
				"Equals" or "StartsWith" or "EndsWith" => method.ContainingType.SpecialType == SpecialType.System_String,
				"Contains" => true,
				"Like" or "ILike" => method.Parameters.Length > 0 && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, dbFunctions),
				_ => false,
			};

		public bool IsTextList(ITypeSymbol? type)
			=> type is not null
				&& type.SpecialType != SpecialType.System_String
				&& ((type is INamedTypeSymbol named && IsEnumerableOfText(named)) || type.AllInterfaces.Any(IsEnumerableOfText));

		private bool IsEnumerableOfText(INamedTypeSymbol type)
			=> SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, genericEnumerable)
				&& type.TypeArguments[0].SpecialType == SpecialType.System_String;

		public bool IsHandle(ISymbol symbol)
			=> symbol is IMethodSymbol method
				&& method.ContainingType.AllInterfaces
					.Where(i => requestHandlers.Contains(i.OriginalDefinition, SymbolEqualityComparer.Default))
					.SelectMany(static i => i.GetMembers("Handle"))
					.Any(handle => SymbolEqualityComparer.Default.Equals(method.ContainingType.FindImplementationForInterfaceMember(handle), method));

		public bool InQueryLambda(IOperation operation)
		{
			for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
			{
				if (parent is IAnonymousFunctionOperation
					&& IsExpression(parent.Parent?.Type)
					&& parent.Parent?.Parent is IArgumentOperation { Parent: IInvocationOperation call }
					&& (IsQueryable(call.Instance?.Type) || (call.Arguments.Length > 0 && IsQueryable(call.Arguments[0].Value.Type))))
					return true;
			}

			return false;
		}

		private bool IsExpression(ITypeSymbol? type)
		{
			for (var current = type; current is not null; current = current.BaseType)
			{
				if (SymbolEqualityComparer.Default.Equals(current, lambdaExpression))
					return true;
			}

			return false;
		}

		private bool IsQueryable(ITypeSymbol? type)
			=> type is not null
				&& (SymbolEqualityComparer.Default.Equals(type, queryable) || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, queryable)));
	}
}
