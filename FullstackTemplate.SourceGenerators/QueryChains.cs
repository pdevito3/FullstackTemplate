namespace FullstackTemplate.SourceGenerators;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

internal enum QueryUse
{
	NotQuery,
	Tagged,
	Untagged,
	Returned,
}

internal sealed class QueryChains(
	INamedTypeSymbol appDbContext,
	INamedTypeSymbol dbSet,
	INamedTypeSymbol queryTagging,
	INamedTypeSymbol queryable,
	INamedTypeSymbol enumerable,
	INamedTypeSymbol asyncEnumerable,
	INamedTypeSymbol lambdaExpression)
{
	public static QueryChains? From(Compilation compilation)
	{
		var appDbContext = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Databases.AppDbContext");
		var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
		var queryTagging = compilation.GetTypeByMetadataName("FullstackTemplate.Server.Databases.QueryTagging");
		var queryable = compilation.GetTypeByMetadataName("System.Linq.IQueryable");
		var enumerable = compilation.GetTypeByMetadataName("System.Collections.IEnumerable");
		var asyncEnumerable = compilation.GetTypeByMetadataName("System.Collections.Generic.IAsyncEnumerable`1");
		var lambdaExpression = compilation.GetTypeByMetadataName("System.Linq.Expressions.LambdaExpression");

		return appDbContext is null || dbSet is null || queryTagging is null || queryable is null || enumerable is null
			|| asyncEnumerable is null || lambdaExpression is null
			? null
			: new QueryChains(appDbContext, dbSet, queryTagging, queryable, enumerable, asyncEnumerable, lambdaExpression);
	}

	public string? DbSetName(IOperation operation) => operation switch
	{
		IPropertyReferenceOperation property when IsDbSet(property.Type) && DerivesFrom(property.Instance?.Type, appDbContext)
			=> property.Property.Name,
		IInvocationOperation { TargetMethod: { Name: "Set", Parameters.Length: 0 } method } set
			when IsDbSet(set.Type) && DerivesFrom(set.Instance?.Type, appDbContext)
			=> $"Set<{method.TypeArguments[0].Name}>()",
		_ => null,
	};

	public ISymbol? HelperOf(IOperation operation)
	{
		var helper = operation switch
		{
			IInvocationOperation invocation => invocation.TargetMethod.OriginalDefinition,
			IPropertyReferenceOperation property => property.Property.GetMethod?.OriginalDefinition,
			_ => null,
		};

		return helper is IMethodSymbol method && !IsTag(method) && IsQueryable(operation.Type) && !IsDbSet(operation.Type)
			&& helper.Locations.Any(static l => l.IsInSource)
			? helper
			: null;
	}

	// EF translates a DbSet inside a query lambda into the SQL of the outer query, which has the tag
	public bool InExpressionTree(IOperation operation)
	{
		for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IAnonymousFunctionOperation && DerivesFrom(parent.Parent?.Type, lambdaExpression))
				return true;
		}

		return false;
	}

	public (QueryUse Use, ISymbol? Owner) Classify(IOperation start, ISymbol containingSymbol)
		=> Classify(start, containingSymbol, ImmutableHashSet<ILocalSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default));

	private (QueryUse Use, ISymbol? Owner) Classify(IOperation start, ISymbol containingSymbol, ImmutableHashSet<ILocalSymbol> followedLocals)
	{
		var current = start;
		var isQuery = !IsDbSet(start.Type);
		while (true)
		{
			var parent = current.Parent;
			while (parent is IConversionOperation or IParenthesizedOperation)
			{
				isQuery |= IsEnumerable(parent.Type);
				current = parent;
				parent = parent.Parent;
			}

			switch (parent)
			{
				case IArgumentOperation { Parameter: { Ordinal: 0 } parameter, Parent: IInvocationOperation call } when call.TargetMethod.IsExtensionMethod:
					if (IsTag(call.TargetMethod))
						return (QueryUse.Tagged, null);
					if (!IsQueryable(call.Type))
						return (isQuery || IsEnumerable(parameter.Type) ? QueryUse.Untagged : QueryUse.NotQuery, null);
					isQuery = true;
					current = call;
					break;
				case IInvocationOperation call when ReferenceEquals(call.Instance, current) && IsEnumerable(call.Type):
					isQuery = true;
					current = call;
					break;
				case IConditionalOperation conditional when !ReferenceEquals(conditional.Condition, current):
					current = conditional;
					break;
				case ICoalesceOperation coalesce:
					current = coalesce;
					break;
				case ISwitchExpressionArmOperation { Parent: ISwitchExpressionOperation switchExpression } arm when ReferenceEquals(arm.Value, current):
					current = switchExpression;
					break;
				case IArgumentOperation { Parent: IInvocationOperation call } argument when isQuery && ReadsAnotherQuery(call, argument):
					return (QueryUse.NotQuery, null);
				case ISimpleAssignmentOperation { Target: ILocalReferenceOperation target, Parent: IExpressionStatementOperation } assignment
					when isQuery && ReferenceEquals(assignment.Value, current):
					return followedLocals.Contains(target.Local)
						? (QueryUse.NotQuery, null)
						: ClassifyLocal(target.Local, assignment, containingSymbol, followedLocals);
				case IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } when isQuery:
					return ClassifyLocal(declarator.Symbol, declarator, containingSymbol, followedLocals);
				case IReturnOperation when isQuery:
					return NamedOwnerOf(parent, containingSymbol) is { } owner ? (QueryUse.Returned, owner) : (QueryUse.Untagged, null);
				default:
					return (isQuery ? QueryUse.Untagged : QueryUse.NotQuery, null);
			}
		}
	}

	private (QueryUse Use, ISymbol? Owner) ClassifyLocal(ILocalSymbol local, IOperation store, ISymbol containingSymbol, ImmutableHashSet<ILocalSymbol> followedLocals)
	{
		var root = store;
		while (root.Parent is not null)
			root = root.Parent;

		followedLocals = followedLocals.Add(local);
		var reads = root.Descendants()
			.OfType<ILocalReferenceOperation>()
			.Where(r => SymbolEqualityComparer.Default.Equals(r.Local, local)
				&& !(r.Parent is ISimpleAssignmentOperation assignment && ReferenceEquals(assignment.Target, r)))
			.ToList();

		ISymbol? returnedFrom = null;
		foreach (var read in reads.Where(r => !InExpressionTree(r)))
		{
			var (use, owner) = Classify(read, containingSymbol, followedLocals);
			if (use == QueryUse.Returned)
				returnedFrom = owner;
			else if (use != QueryUse.NotQuery)
				return (QueryUse.Untagged, null);
		}

		if (returnedFrom is not null)
			return (QueryUse.Returned, returnedFrom);

		return reads.Count > 0 ? (QueryUse.NotQuery, null) : (QueryUse.Untagged, null);
	}

	private bool ReadsAnotherQuery(IInvocationOperation call, IArgumentOperation argument)
		=> IsQueryable(call.Instance?.Type)
			|| (call.Arguments.FirstOrDefault() is { } source && !ReferenceEquals(source, argument) && IsQueryable(source.Value.Type));

	private static ISymbol? NamedOwnerOf(IOperation operation, ISymbol containingSymbol)
	{
		for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
		{
			if (parent is IAnonymousFunctionOperation)
				return null;
			if (parent is ILocalFunctionOperation local)
				return local.Symbol.OriginalDefinition;
		}

		return containingSymbol is IMethodSymbol method ? method.OriginalDefinition : null;
	}

	private bool IsDbSet(ITypeSymbol? type)
		=> type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dbSet);

	private bool IsTag(IMethodSymbol method)
		=> method.Name == "TagWithOperationCallSite" && SymbolEqualityComparer.Default.Equals(method.ContainingType, queryTagging);

	private bool IsQueryable(ITypeSymbol? type) => Implements(type, queryable);

	private bool IsEnumerable(ITypeSymbol? type)
		=> Implements(type, enumerable)
			|| (type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, asyncEnumerable))
			|| type?.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, asyncEnumerable)) == true;

	private static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol target)
	{
		for (var current = type; current is not null; current = current.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(current, target))
				return true;
		}

		return false;
	}

	private static bool Implements(ITypeSymbol? type, INamedTypeSymbol target)
		=> type is not null
			&& (SymbolEqualityComparer.Default.Equals(type, target) || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, target)));
}
