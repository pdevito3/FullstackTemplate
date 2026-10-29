namespace FullstackTemplate.SourceGenerators;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

/// <summary>
/// Checks the text of each message that a user can read for a code name or an ID (docs/messages.md, rule 1). A user
/// message is a string that goes to a parameter named <c>message</c> or <c>*Message</c> of a method of the project, such as
/// the constructor of an exception or <c>ForRequestField</c>, to a FluentValidation <c>WithMessage</c>, or to a string field
/// whose name ends in <c>Message</c>. The analyzer follows the string back through the methods, properties, fields, locals,
/// and parameters of the project, so a message that a helper builds is checked too.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UserMessageAnalyzer : DiagnosticAnalyzer
{
	private const string Fix = "Name it as the screen names it, for example \"You cannot manage users.\" See docs/messages.md.";

	internal static readonly DiagnosticDescriptor CodeName = new(
		id: "FSTMSG001",
		title: "A user message shows a code name",
		messageFormat: "A user message shows {0}. " + Fix,
		category: "UserMessage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: WellKnownDiagnosticTags.CompilationEnd);

	internal static readonly DiagnosticDescriptor Id = new(
		id: "FSTMSG002",
		title: "A user message shows an ID",
		messageFormat: "A user message shows the ID '{0}'. Name the record as the screen names it, or leave it out. See docs/messages.md.",
		category: "UserMessage",
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true,
		customTags: WellKnownDiagnosticTags.CompilationEnd);

	// A snake_case word, such as manage_users, or a PascalCase word with two or more parts, such as CheckedIn. A word in
	// braces, such as {PropertyName}, is a placeholder that FluentValidation fills with words.
	private static readonly Regex CodeWord = new(
		@"\b(?<!\{)(?:[a-z][a-z0-9]*(?:_[a-z0-9]+)+|[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]+)+)\b(?!\})",
		RegexOptions.CultureInvariant);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(CodeName, Id);

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(static start =>
		{
			var flow = new StringFlow();
			start.RegisterOperationAction(flow.Record, OperationKind.Return, OperationKind.FieldInitializer,
				OperationKind.PropertyInitializer, OperationKind.VariableDeclarator, OperationKind.SimpleAssignment,
				OperationKind.Argument);
			start.RegisterCompilationEndAction(flow.Report);
		});
	}

	/// <summary>
	/// The string values that each symbol of the project can hold, and the values that reach a user message. The checks
	/// run at the end of the compilation, when the values of each helper are known.
	/// </summary>
	private sealed class StringFlow
	{
		private readonly ConcurrentBag<IOperation> _messages = new();
		private readonly ConcurrentDictionary<ISymbol, ConcurrentBag<IOperation>> _values = new(SymbolEqualityComparer.Default);

		public void Record(OperationAnalysisContext ctx)
		{
			switch (ctx.Operation)
			{
				case IReturnOperation { ReturnedValue: { } value }:
					Add(OwnerOf(ctx.Operation, ctx.ContainingSymbol), value);
					break;
				case IFieldInitializerOperation initializer:
					foreach (var field in initializer.InitializedFields)
					{
						Add(field, initializer.Value);
						if (field.Type.SpecialType == SpecialType.System_String && field.Name.EndsWith("Message", StringComparison.Ordinal))
							_messages.Add(initializer.Value);
					}
					break;
				case IPropertyInitializerOperation initializer:
					foreach (var property in initializer.InitializedProperties)
					{
						if (property.GetMethod is { } getter)
							Add(getter, initializer.Value);
						if (property.Type.SpecialType == SpecialType.System_String && property.Name.EndsWith("Message", StringComparison.Ordinal))
							_messages.Add(initializer.Value);
					}
					break;
				case IVariableDeclaratorOperation { Initializer: { } initializer } declarator:
					Add(declarator.Symbol, initializer.Value);
					break;
				case ISimpleAssignmentOperation { Target: ILocalReferenceOperation local } assignment:
					Add(local.Local, assignment.Value);
					break;
				case ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property: var property } } assignment
					when IsProblemText(property) || IsErrorToolText(property, assignment):
					_messages.Add(assignment.Value);
					break;
				case IArgumentOperation { Parameter: { Type.SpecialType: SpecialType.System_String } parameter } argument:
					Add(parameter.OriginalDefinition, argument.Value);
					if (IsMessageParameter(parameter, argument))
						_messages.Add(argument.Value);
					break;
				case IArgumentOperation { Parameter.ContainingSymbol: IMethodSymbol { Name: "WithMessage" } method, Value: IDelegateCreationOperation { Target: IAnonymousFunctionOperation lambda } }
					when IsIn(method.ContainingType, "FluentValidation"):
					foreach (var returned in lambda.Body.Descendants().OfType<IReturnOperation>())
					{
						if (returned.ReturnedValue is { } value && SymbolEqualityComparer.Default.Equals(OwnerOf(returned, lambda.Symbol), lambda.Symbol))
							_messages.Add(value);
					}
					break;
			}
		}

		public void Report(CompilationAnalysisContext ctx)
		{
			var walk = new Walk(_values, ctx);
			foreach (var message in _messages)
				walk.Visit(message);
		}

		private void Add(ISymbol symbol, IOperation value)
		{
			if (value.Type?.SpecialType == SpecialType.System_String)
				_values.GetOrAdd(symbol.OriginalDefinition, static _ => new ConcurrentBag<IOperation>()).Add(value);
		}

		// A return inside a lambda or a local function is a value of that function, not of the method around it
		private static ISymbol OwnerOf(IOperation operation, ISymbol containingSymbol)
		{
			for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
			{
				if (parent is IAnonymousFunctionOperation lambda)
					return lambda.Symbol;
				if (parent is ILocalFunctionOperation local)
					return local.Symbol;
			}

			return containingSymbol;
		}

		private static bool IsMessageParameter(IParameterSymbol parameter, IArgumentOperation argument)
		{
			if (parameter.ContainingSymbol is not IMethodSymbol method)
				return false;
			if (method.Name == "WithMessage" && IsIn(method.ContainingType, "FluentValidation"))
				return true;
			if (method.ContainingType is { Name: "TypedResults" or "Results" } results && IsIn(results, "Microsoft.AspNetCore.Http"))
			{
				return method.Name is "Problem" or "ValidationProblem"
					? parameter.Name is "detail" or "title"
					: method.Name is "BadRequest" or "Conflict" or "NotFound" or "UnprocessableEntity" or "InternalServerError";
			}

			var inSource = method.Locations.Any(static l => l.IsInSource);
			if (method.MethodKind == MethodKind.Constructor && parameter.Name == "message" && !inSource)
			{
				// The SDK sends the text of an McpException to the client. Another exception of a package is a user message
				// only as the base of an exception of the project, because only the exceptions of the project map to a problem.
				return DerivesFrom(method.ContainingType, "ModelContextProtocol", "McpException")
					|| (argument.Parent is IInvocationOperation && DerivesFrom(method.ContainingType, "System", "Exception"));
			}

			return inSource && (parameter.Name == "message" || parameter.Name.EndsWith("Message", StringComparison.Ordinal));
		}

		private static bool IsProblemText(IPropertySymbol property)
			=> property.Name is "Detail" or "Title" && DerivesFrom(property.ContainingType, "Microsoft.AspNetCore.Mvc", "ProblemDetails");

		// The text of a tool result that is set in the initializer of a result with IsError = true
		private static bool IsErrorToolText(IPropertySymbol property, IOperation assignment)
		{
			if (property.Name != "Text" || !DerivesFrom(property.ContainingType, "ModelContextProtocol.Protocol", "TextContentBlock"))
				return false;

			for (var parent = assignment.Parent; parent is not null; parent = parent.Parent)
			{
				if (parent is IObjectCreationOperation { Type: { } type, Initializer: { } initializer }
					&& DerivesFrom(type, "ModelContextProtocol.Protocol", "CallToolResult"))
				{
					// IsError is a bool?, so the true is in a conversion
					return initializer.Initializers.Any(static i => i is ISimpleAssignmentOperation
					{
						Target: IPropertyReferenceOperation { Property.Name: "IsError" },
						Value: IConversionOperation { Operand.ConstantValue: { HasValue: true, Value: true } } or { ConstantValue: { HasValue: true, Value: true } },
					});
				}
			}

			return false;
		}
	}

	private static bool DerivesFrom(ITypeSymbol? type, string ns, string name)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (current.Name == name && current.ContainingNamespace?.ToDisplayString() == ns)
				return true;
		}

		return false;
	}

	private static bool IsIn(ITypeSymbol? type, string ns)
		=> type?.ContainingNamespace?.ToDisplayString() is { } name
			&& (name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal));

	/// <summary>
	/// Visits the values that reach a user message, and reports each code name and ID in them once. Each value is checked
	/// as it is and through each local, parameter, field, getter, and method that it comes from. A parameter of a helper
	/// that the message calls is followed to the argument of that call only, so another call of the helper is not checked.
	/// </summary>
	private sealed class Walk(ConcurrentDictionary<ISymbol, ConcurrentBag<IOperation>> values, CompilationAnalysisContext ctx)
	{
		private const int MaxCallDepth = 8;

		private readonly HashSet<(ISymbol, Call?)> _followed = new();
		private readonly HashSet<(string, Location)> _reported = new();

		public void Visit(IOperation? operation, Call? call = null)
		{
			if (operation is IConversionOperation { Type: { } target } && IsNumber(target))
				return;

			var value = Unwrap(operation);
			if (value is null || ReportedAsShown(value))
				return;

			switch (value)
			{
				case ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } literal:
					CheckText(text, literal.Syntax.GetLocation());
					break;
				case IInterpolatedStringOperation interpolated:
					foreach (var part in interpolated.Parts)
					{
						if (part is IInterpolatedStringTextOperation { Text.ConstantValue: { HasValue: true, Value: string text } })
							CheckText(text, part.Syntax.GetLocation());
						else if (part is IInterpolationOperation hole)
							Visit(hole.Expression, call);
					}
					break;
				case IBinaryOperation binary:
					Visit(binary.LeftOperand, call);
					Visit(binary.RightOperand, call);
					break;
				case IConditionalOperation conditional:
					Visit(conditional.WhenTrue, call);
					Visit(conditional.WhenFalse, call);
					break;
				case ICoalesceOperation coalesce:
					Visit(coalesce.Value, call);
					Visit(coalesce.WhenNull, call);
					break;
				case ISwitchExpressionOperation switchExpression:
					foreach (var arm in switchExpression.Arms)
						Visit(arm.Value, call);
					break;
				case IInvocationOperation invocation when invocation.TargetMethod.ContainingType?.SpecialType == SpecialType.System_String:
					Visit(invocation.Instance, call);
					foreach (var argument in invocation.Arguments)
						Visit(argument.Value, call);
					break;
				case IInvocationOperation invocation when (call?.Depth ?? 0) < MaxCallDepth:
					Follow(invocation.TargetMethod, new Call(invocation, call));
					break;
				case IPropertyReferenceOperation { Property.GetMethod: { } getter }:
					Follow(getter, call);
					break;
				case IFieldReferenceOperation field:
					Follow(field.Field, call);
					break;
				case ILocalReferenceOperation local:
					Follow(local.Local, call);
					break;
				case IParameterReferenceOperation parameter:
					FollowParameter(parameter, call);
					break;
			}
		}

		private void Follow(ISymbol symbol, Call? call)
		{
			if (!_followed.Add((symbol.OriginalDefinition, call)) || !values.TryGetValue(symbol.OriginalDefinition, out var found))
				return;

			foreach (var value in found)
				Visit(value, call);
		}

		// Inside a helper that the message calls, the parameter holds the argument of that call. Outside one, as for the
		// message parameter of a guard, it holds the argument of each call.
		private void FollowParameter(IParameterReferenceOperation reference, Call? call)
		{
			var parameter = reference.Parameter;
			for (var frame = call; frame is not null; frame = frame.Caller)
			{
				if (!SymbolEqualityComparer.Default.Equals(frame.Invocation.TargetMethod.OriginalDefinition, parameter.ContainingSymbol.OriginalDefinition))
					continue;

				var argument = frame.Invocation.Arguments.FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.Parameter?.OriginalDefinition, parameter.OriginalDefinition));
				Visit(argument?.Value, frame.Caller);
				return;
			}

			if (!IsRejectedByParse(reference))
				Follow(parameter, null);
		}

		// In "Invalid permission: {value}" after Permission.TryFromName(value, ...) fails, the value is not a name that the
		// parse accepts, so it is the text that the user sent. A caller that passes a name, such as Permission.ManageUsers(),
		// never gets to the message.
		private static bool IsRejectedByParse(IParameterReferenceOperation reference)
		{
			var thrown = false;
			for (var parent = reference.Parent; parent is not null; parent = parent.Parent)
			{
				if (parent is IThrowOperation)
					thrown = true;
				else if (thrown && parent is IConditionalOperation conditional && conditional.Condition.DescendantsAndSelf().Any(o => IsParseOf(o, reference.Parameter)))
					return true;
			}

			return false;
		}

		private static bool IsParseOf(IOperation operation, IParameterSymbol parameter)
			=> operation is IInvocationOperation { TargetMethod.ContainingType: { } type } invocation
				&& (type.SpecialType == SpecialType.System_Enum || IsFixedName(type))
				&& invocation.Arguments.Any(a => Unwrap(a.Value) is IParameterReferenceOperation p && SymbolEqualityComparer.Default.Equals(p.Parameter, parameter));

		// A value that is reported is not followed further, so the getter of a code does not get a second report
		private bool ReportedAsShown(IOperation value)
		{
			var text = value.Syntax.ToString();
			if (CodeOf(value) is { } code)
				Report(CodeName, value.Syntax.GetLocation(), $"{code} '{text}'");
			else if (IsId(value))
				Report(Id, value.Syntax.GetLocation(), text);
			else
				return false;

			return true;
		}

		private void CheckText(string text, Location location)
		{
			foreach (Match match in CodeWord.Matches(text))
				Report(CodeName, location, $"the code name '{match.Value}'");
		}

		// A getter or a method of the project that returns a code shows that code too, as Value => _status.Name does
		private string? CodeOf(IOperation? value, int depth = 0)
		{
			switch (value)
			{
				case INameOfOperation:
					return "the code name";
				case { Type.TypeKind: TypeKind.Enum }:
					return "the enum value";
				case { Type: { } type } when IsFixedName(type):
					return "the stored name";
				case IInvocationOperation { TargetMethod.Name: "ToString", Instance.Type: { } type } when type.TypeKind == TypeKind.Enum || IsFixedName(type):
					return "the stored name";
				case IPropertyReferenceOperation { Property.Name: "Value" or "Name" } property when IsFixedName(property.Instance?.Type ?? property.Property.ContainingType):
					return "the stored name";
				case IPropertyReferenceOperation { Property.Name: "Name" or "FullName" } property when IsReflectionType(property.Instance?.Type ?? property.Property.ContainingType):
					return "the type name";
				case IInvocationOperation { TargetMethod: { Name: "GetName" or "GetNames", ContainingType.SpecialType: SpecialType.System_Enum } }:
					return "the enum value";
				case { Type: { } type } when ElementOf(type) is { } element && (element.TypeKind == TypeKind.Enum || IsFixedName(element)):
					return element.TypeKind == TypeKind.Enum ? "the enum value" : "the stored name";
				case IPropertyReferenceOperation { Property.GetMethod: { } getter } when depth < 3:
					return CodeOfReturns(getter, depth);
				case IInvocationOperation invocation when depth < 3:
					return CodeOfReturns(invocation.TargetMethod, depth);
				default:
					return null;
			}
		}

		private string? CodeOfReturns(IMethodSymbol method, int depth)
			=> values.TryGetValue(method.OriginalDefinition, out var returned)
				? returned.Select(v => CodeOf(Unwrap(v), depth + 1)).Where(static c => c is not null).OrderBy(static c => c, StringComparer.Ordinal).FirstOrDefault()
				: null;

		private static bool IsId(IOperation? value)
		{
			var name = value switch
			{
				IPropertyReferenceOperation property => property.Property.Name,
				IFieldReferenceOperation field => field.Field.Name,
				ILocalReferenceOperation local => local.Local.Name,
				IParameterReferenceOperation parameter => parameter.Parameter.Name,
				_ => null,
			};

			return IsIdName(name) || IsIdName(value?.Type?.Name);
		}

		// Id, TenantId, and clientId, but not Valid or Paid
		private static bool IsIdName(string? name)
			=> name is not null && (name is "id" or "Id" || (name.Length > 2 && name.EndsWith("Id", StringComparison.Ordinal)));

		// A value object with a fixed set of names, or an Ardalis SmartEnum
		private static bool IsFixedName(ITypeSymbol? type)
		{
			if (type is null)
				return false;
			if (type.AllInterfaces.Any(static i => i.Name == "IFixedNames"))
				return true;

			for (var current = type; current is not null; current = current.BaseType)
			{
				if (current.Name == "SmartEnum")
					return true;
			}

			return false;
		}

		// The item type of an array or a collection, such as the result of Enum.GetValues<T>()
		private static ITypeSymbol? ElementOf(ITypeSymbol type)
		{
			if (type is IArrayTypeSymbol array)
				return array.ElementType;
			if (type.SpecialType == SpecialType.System_String)
				return null;

			if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Collections_Generic_IEnumerable_T } enumerable)
				return enumerable.TypeArguments[0];

			return type.AllInterfaces
				.FirstOrDefault(static i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
				?.TypeArguments[0];
		}

		private static bool IsReflectionType(ITypeSymbol? type)
			=> type?.ToDisplayString() is "System.Type" or "System.Reflection.MemberInfo" or "System.Reflection.TypeInfo";

		private static bool IsNumber(ITypeSymbol type)
			=> type.SpecialType is >= SpecialType.System_SByte and <= SpecialType.System_Double;

		private static IOperation? Unwrap(IOperation? operation)
		{
			while (operation is IConversionOperation or IParenthesizedOperation)
				operation = operation is IConversionOperation conversion ? conversion.Operand : ((IParenthesizedOperation)operation).Operand;

			return operation;
		}

		private void Report(DiagnosticDescriptor descriptor, Location location, string shown)
		{
			if (_reported.Add((descriptor.Id + shown, location)))
				ctx.ReportDiagnostic(Diagnostic.Create(descriptor, location, shown));
		}
	}

	/// <summary>A call of a project method on the way from a user message to a value, and the call that it is in.</summary>
	private sealed class Call(IInvocationOperation invocation, Call? caller)
	{
		public IInvocationOperation Invocation { get; } = invocation;

		public Call? Caller { get; } = caller;

		public int Depth { get; } = (caller?.Depth ?? 0) + 1;
	}
}
