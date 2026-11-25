namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Refuses the copies that a sequence does not need: a ToList or a ToArray in the middle of a LINQ chain, an Aggregate
/// that copies its accumulator on each element, and a string += in a loop. See "Collections" in
/// docs/backend/code-style.md.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LinqCopyAnalyzer : DiagnosticAnalyzer
{
	internal static readonly DiagnosticDescriptor MidChainCopy = new(
		id: "FSTLNQ001",
		title: "A LINQ chain copies its elements in the middle",
		messageFormat: "{0}() copies the elements, and {1} reads the copy one time. Put the copy at the end of the chain, or call AsEnumerable() to switch from a query to memory. See docs/backend/code-style.md.",
		category: "Linq",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor AccumulatorCopy = new(
		id: "FSTLNQ002",
		title: "An Aggregate copies its accumulator",
		messageFormat: "The Aggregate makes a new {0} for each element, so it takes time in the square of the count. Use a loop, SelectMany, string.Join, or a builder. See docs/backend/code-style.md.",
		category: "Linq",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	internal static readonly DiagnosticDescriptor StringAppendInLoop = new(
		id: "FSTLNQ003",
		title: "A loop appends to a string",
		messageFormat: "'{0}' copies the whole string on each pass of the loop. Use a StringBuilder or string.Join. See docs/backend/code-style.md.",
		category: "Linq",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: [RuleScope.AllCode]);

	private static readonly ImmutableHashSet<string> Copies = ["ToList", "ToArray", "ToHashSet"];

	private static readonly ImmutableHashSet<string> CollectionCopies =
		["Concat", "Append", "Prepend", "Union", "ToList", "ToArray", "ToHashSet", "ToImmutableList", "ToImmutableArray"];

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MidChainCopy, AccumulatorCopy, StringAppendInLoop];

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
		context.RegisterOperationAction(AnalyzeCompoundAssignment, OperationKind.CompoundAssignment);
	}

	private static void AnalyzeInvocation(OperationAnalysisContext ctx)
	{
		var call = (IInvocationOperation)ctx.Operation;
		if (!IsEnumerable(call.TargetMethod))
			return;

		if (Copies.Contains(call.TargetMethod.Name) && NextInChain(call) is { } next)
			ctx.ReportDiagnostic(Diagnostic.Create(MidChainCopy, call.Syntax.GetLocation(), call.TargetMethod.Name, next.TargetMethod.Name));

		if (call.TargetMethod.Name == "Aggregate" && CopiedAccumulator(call) is { } copied)
			ctx.ReportDiagnostic(Diagnostic.Create(AccumulatorCopy, call.Syntax.GetLocation(), copied));
	}

	/// <summary>The Enumerable call that takes the result of <paramref name="call"/> as its source.</summary>
	private static IInvocationOperation? NextInChain(IInvocationOperation call)
	{
		var parent = call.Parent;
		while (parent is IConversionOperation)
			parent = parent.Parent;

		return parent is IArgumentOperation { Parent: IInvocationOperation next } argument
			&& IsEnumerable(next.TargetMethod)
			&& next.Arguments.FirstOrDefault() == argument
				? next
				: null;
	}

	/// <summary>The kind of value that the lambda of an Aggregate copies from its accumulator, or null.</summary>
	private static string? CopiedAccumulator(IInvocationOperation call)
	{
		var lambda = call.Arguments
			.Select(argument => argument.Value)
			.OfType<IDelegateCreationOperation>()
			.Select(creation => creation.Target)
			.OfType<IAnonymousFunctionOperation>()
			.FirstOrDefault();
		if (lambda is null || lambda.Symbol.Parameters.Length < 2)
			return null;

		var accumulator = lambda.Symbol.Parameters[0];
		bool ReadsAccumulator(IOperation operation) => operation.DescendantsAndSelf()
			.OfType<IParameterReferenceOperation>()
			.Any(reference => SymbolEqualityComparer.Default.Equals(reference.Parameter, accumulator));

		foreach (var operation in lambda.Body.Descendants())
		{
			if (operation is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, Type.SpecialType: SpecialType.System_String } concat
				&& (ReadsAccumulator(concat.LeftOperand) || ReadsAccumulator(concat.RightOperand)))
				return "string";

			if (operation is IInvocationOperation copy && ReadsAccumulator(copy) && IsCollectionCopy(copy.TargetMethod))
				return "collection";
		}

		return null;
	}

	private static bool IsCollectionCopy(IMethodSymbol method) =>
		CollectionCopies.Contains(method.Name)
		|| (method.Name is "Add" or "AddRange" && method.ContainingType?.OriginalDefinition.ToDisplayString() == "System.Collections.Immutable.ImmutableArray<T>");

	private static void AnalyzeCompoundAssignment(OperationAnalysisContext ctx)
	{
		var assignment = (ICompoundAssignmentOperation)ctx.Operation;
		if (assignment.OperatorKind != BinaryOperatorKind.Add || assignment.Type?.SpecialType != SpecialType.System_String)
			return;

		for (var parent = assignment.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
				return;
			if (parent is ILoopOperation loop)
			{
				// A local of the loop body starts again on each pass, so it does not grow
				if (assignment.Target is ILocalReferenceOperation { Local: var local }
					&& local.DeclaringSyntaxReferences.Any(r => loop.Body.Syntax.Span.Contains(r.Span)))
					return;

				ctx.ReportDiagnostic(Diagnostic.Create(StringAppendInLoop, assignment.Syntax.GetLocation(), assignment.Syntax.ToString()));
				return;
			}
		}
	}

	private static bool IsEnumerable(IMethodSymbol method) =>
		method.ContainingType?.ToDisplayString() == "System.Linq.Enumerable";
}
