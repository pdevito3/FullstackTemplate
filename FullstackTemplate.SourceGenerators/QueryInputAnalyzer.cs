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
			IInvocationOperation call when types.SendsArguments(call.TargetMethod) && !IsEqualsWithNull(call)
				=> [.. call.Arguments.Select(static a => a.Value), .. call.Instance is null ? [] : new[] { call.Instance }],
			_ => [],
		};

		foreach (var operand in operands.SelectMany(o => TextsIn(o, types)))
		{
			if (PathFromFirstParameter(operand, types) is not { } path || !types.IsHandle(path[0].ContainingSymbol) || !types.InQueryLambda(ctx.Operation))
				continue;

			if (!IsCheckedBefore(operand, path, types))
				ctx.ReportDiagnostic(Diagnostic.Create(RawRequestText, operand.Syntax.GetLocation(), operand.Syntax.ToString()));
		}
	}

	private static IEnumerable<IOperation> TextsIn(IOperation operand, InputTypes types)
	{
		yield return operand;
		var parts = Unwrap(operand) switch
		{
			IInterpolatedStringOperation interpolated => interpolated.Parts.OfType<IInterpolationOperation>().Select(static p => p.Expression),
			IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } concat => [concat.LeftOperand, concat.RightOperand],
			IInvocationOperation call when types.IsLikePattern(call.TargetMethod) || BuildsText(call.TargetMethod) => call.Arguments.Select(static a => a.Value),
			IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
			ICoalesceOperation coalesce => [coalesce.Value, coalesce.WhenNull],
			IConditionalOperation { WhenFalse: { } whenFalse } conditional => [conditional.WhenTrue, whenFalse],
			_ => [],
		};

		foreach (var text in parts.SelectMany(p => TextsIn(p, types)))
			yield return text;
	}

	// EF evaluates a compare of a request value with null on the client, so the text does not go to Postgres
	private static bool IsNull(IOperation operand) => operand.ConstantValue is { HasValue: true, Value: null };

	private static bool IsEqualsWithNull(IInvocationOperation call)
		=> call.TargetMethod.Name == "Equals" && call.Arguments.Any(static a => IsNull(a.Value));

	private static bool BuildsText(IMethodSymbol method)
		=> method.ContainingType.SpecialType == SpecialType.System_String && method.Name is "Concat" or "Format";

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
				case IConditionalAccessOperation access:
					value = Unwrap(access.WhenNotNull);
					break;
				case IConditionalAccessInstanceOperation instance when ReceiverOf(instance) is { } receiver:
					value = Unwrap(receiver);
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

	private static IOperation? ReceiverOf(IConditionalAccessInstanceOperation instance)
	{
		IOperation child = instance;
		for (var parent = instance.Parent; parent is not null; child = parent, parent = parent.Parent)
		{
			if (parent is IConditionalAccessOperation access && ReferenceEquals(access.WhenNotNull, child))
				return access.Operation;
		}

		return null;
	}

	private static IOperation Unwrap(IOperation operand)
	{
		var value = operand;
		while (true)
		{
			switch (value)
			{
				case IConversionOperation conversion when conversion.IsImplicit || conversion.Conversion is { IsIdentity: true } or { IsReference: true }:
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

	private static bool IsCheckedBefore(IOperation text, List<ISymbol> path, InputTypes types)
	{
		var root = text;
		while (root.Parent is not null)
			root = root.Parent;

		return root.Descendants()
			.Where(check => check.Syntax.SpanStart < text.Syntax.SpanStart)
			.SelectMany(check => CheckedValues(check, root, types))
			.Any(value => IsAtPath(value, path, types) || ChecksExpressionThatHolds(value, text, path, types));
	}

	private static bool IsAtPath(IOperation value, List<ISymbol> path, InputTypes types)
		=> PathFromFirstParameter(value, types) is { } other && other.SequenceEqual(path, SymbolEqualityComparer.Default);

	// The check of `a ?? b` does not cover a compare of `b` alone, but it covers a compare of the same `a ?? b`.
	private static bool ChecksExpressionThatHolds(IOperation value, IOperation text, List<ISymbol> path, InputTypes types)
	{
		if (!TextsIn(value, types).Any(inner => IsAtPath(inner, path, types)))
			return false;

		var expression = Unwrap(value).Syntax;
		for (var parent = text.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent.Syntax.IsEquivalentTo(expression))
				return true;
		}

		return false;
	}

	private static IEnumerable<IOperation> CheckedValues(IOperation operation, IOperation root, InputTypes types)
	{
		switch (operation)
		{
			case IInvocationOperation call when types.IsCheck(call.TargetMethod):
				foreach (var argument in call.Arguments)
				{
					yield return argument.Value;
					if (Unwrap(argument.Value) is ICoalesceOperation coalesce)
						yield return coalesce.Value;
					if (ListThatHoldsItem(argument.Value, root, types) is { } list)
						yield return list;
				}

				break;
			case IMethodReferenceOperation reference when types.IsCheck(reference.Method) && SourceOf(reference.Parent) is { } source:
				yield return source;
				break;
		}
	}

	private static IOperation? ListThatHoldsItem(IOperation value, IOperation root, InputTypes types)
	{
		ISymbol item;
		switch (Unwrap(value))
		{
			case IArrayElementReferenceOperation { Indices.Length: 1 } element when InLoopOverEachItem(element, element.ArrayReference, element.Indices[0], types):
				return element.ArrayReference;
			case IPropertyReferenceOperation { Property.IsIndexer: true, Instance: { } list, Arguments.Length: 1 } element
				when InLoopOverEachItem(element, list, element.Arguments[0].Value, types):
				return list;
			case ILocalReferenceOperation local when InitializerIfNeverReassigned(local.Local, root) is { } initializer:
				return ListThatHoldsItem(initializer, root, types);
			case IParameterReferenceOperation parameter when !IsAssigned(parameter.Parameter, root):
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

	private static bool InLoopOverEachItem(IOperation item, IOperation list, IOperation index, InputTypes types)
	{
		if (Unwrap(index) is not ILocalReferenceOperation { Local: var local } || PathFromFirstParameter(list, types) is not { } path)
			return false;

		for (var parent = item.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IForLoopOperation loop && loop.Locals.Contains(local, SymbolEqualityComparer.Default))
				return ReachesEachItem(loop, local, path, types);
		}

		return false;
	}

	private static bool ReachesEachItem(IForLoopOperation loop, ILocalSymbol index, List<ISymbol> list, InputTypes types)
	{
		if (loop.AtLoopBottom.Length != 1 || loop.AtLoopBottom[0] is not IExpressionStatementOperation { Operation: var step } || IsAssigned(index, loop.Body))
			return false;

		var start = loop.Before
			.OfType<IVariableDeclarationGroupOperation>()
			.SelectMany(static group => group.Declarations)
			.SelectMany(static declaration => declaration.Declarators)
			.FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Symbol, index))
			?.Initializer?.Value;

		return StepOf(step, index) switch
		{
			1 => IsNumber(start, 0)
				&& loop.Condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.LessThan } below
				&& IsIndex(below.LeftOperand, index)
				&& IsSizeOf(below.RightOperand, list, types),
			-1 => start is IBinaryOperation { OperatorKind: BinaryOperatorKind.Subtract } last
				&& IsNumber(last.RightOperand, 1)
				&& IsSizeOf(last.LeftOperand, list, types)
				&& loop.Condition is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThanOrEqual } above
				&& IsIndex(above.LeftOperand, index)
				&& IsNumber(above.RightOperand, 0),
			_ => false,
		};
	}

	private static int StepOf(IOperation step, ILocalSymbol index)
		=> step switch
		{
			IIncrementOrDecrementOperation change when IsIndex(change.Target, index) => change.Kind == OperationKind.Increment ? 1 : -1,
			ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Add } change when IsIndex(change.Target, index) && IsNumber(change.Value, 1) => 1,
			ICompoundAssignmentOperation { OperatorKind: BinaryOperatorKind.Subtract } change when IsIndex(change.Target, index) && IsNumber(change.Value, 1) => -1,
			_ => 0,
		};

	private static bool IsNumber(IOperation? operation, int number)
		=> operation?.ConstantValue is { HasValue: true, Value: int value } && value == number;

	private static bool IsIndex(IOperation operation, ILocalSymbol index)
		=> Unwrap(operation) is ILocalReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Local, index);

	private static bool IsSizeOf(IOperation operation, List<ISymbol> list, InputTypes types)
		=> Unwrap(operation) is IPropertyReferenceOperation { Property.Name: "Length" or "Count", Instance: { } owner }
			&& PathFromFirstParameter(owner, types) is { } path
			&& path.SequenceEqual(list, SymbolEqualityComparer.Default);

	private static IOperation? InitializerIfNeverReassigned(ILocalSymbol local, IOperation root)
		=> IsAssigned(local, root)
			? null
			: root.Descendants()
				.OfType<IVariableDeclaratorOperation>()
				.FirstOrDefault(d => SymbolEqualityComparer.Default.Equals(d.Symbol, local))
				?.Initializer?.Value;

	private static bool IsAssigned(ISymbol variable, IOperation scope)
		=> scope.Descendants().Any(reference => reference switch
		{
			ILocalReferenceOperation local => SymbolEqualityComparer.Default.Equals(local.Local, variable) && IsWrite(local),
			IParameterReferenceOperation parameter => SymbolEqualityComparer.Default.Equals(parameter.Parameter, variable) && IsWrite(parameter),
			_ => false,
		});

	private static bool IsWrite(IOperation reference)
	{
		var target = reference;
		while (target.Parent is ITupleOperation tuple)
			target = tuple;

		return target.Parent switch
		{
			IAssignmentOperation assignment => ReferenceEquals(assignment.Target, target) || assignment is ISimpleAssignmentOperation { IsRef: true },
			IVariableInitializerOperation { Parent: IVariableDeclaratorOperation { Symbol.RefKind: RefKind.Ref } } => true,
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
		if (list is IConditionalAccessInstanceOperation instance && ReceiverOf(instance) is { } receiver)
			list = Unwrap(receiver);

		return list is ICoalesceOperation coalesce ? coalesce.Value : list;
	}

	private sealed class InputTypes(
		INamedTypeSymbol entityId,
		INamedTypeSymbol postgresText,
		INamedTypeSymbol dbFunctions,
		INamedTypeSymbol genericEnumerable,
		INamedTypeSymbol queryable,
		INamedTypeSymbol lambdaExpression,
		INamedTypeSymbol? likePattern,
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
			var likePattern = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Databases.LikePattern");
			var requestHandlers = new[] { "MediatR.IRequestHandler`1", "MediatR.IRequestHandler`2" }
				.Select(compilation.GetTypeByMetadataName)
				.OfType<INamedTypeSymbol>()
				.ToImmutableArray();

			return entityId is null || postgresText is null || dbFunctions is null || queryable is null || lambdaExpression is null || requestHandlers.IsEmpty
				? null
				: new InputTypes(entityId, postgresText, dbFunctions, genericEnumerable, queryable, lambdaExpression, likePattern, requestHandlers);
		}

		public bool IsCheck(IMethodSymbol method)
			=> (method.Name == "IsValid" && SymbolEqualityComparer.Default.Equals(method.ContainingType, entityId))
				|| (method.Name == "CanStore" && SymbolEqualityComparer.Default.Equals(method.ContainingType, postgresText));

		public bool IsLikePattern(IMethodSymbol method)
			=> likePattern is not null && SymbolEqualityComparer.Default.Equals(method.ContainingType, likePattern);

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
