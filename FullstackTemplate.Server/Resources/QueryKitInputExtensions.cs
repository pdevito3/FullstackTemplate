namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using System.Linq.Expressions;
using Databases;
using Domain;
using Exceptions;
using QueryKit;
using QueryKit.Configuration;
using QueryKit.Exceptions;

/// <summary>
/// Applies the <c>filters</c> and <c>sortOrder</c> values of a list request with QueryKit.
/// A value that QueryKit cannot apply, or that uses the version, throws a <see cref="QueryInputException"/> with the name of its parameter.
/// </summary>
public static class QueryKitInputExtensions
{
    public const string FiltersParameter = "filters";
    public const string SortOrderParameter = "sortOrder";

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, string filters, IQueryKitConfiguration config)
        where T : class
        => Apply(FiltersParameter, () =>
        {
            // A value of the filter goes to the query as a parameter, so it must be text that Postgres can take
            if (!PostgresText.CanStore(filters))
                throw new QueryInputException(FiltersParameter, $"The {FiltersParameter} value has a character that is not allowed.");
            var predicate = FilterParser.ParseFilter<T>(filters, config);
            RefuseVersion(FiltersParameter, predicate);
            return query.Where(predicate);
        });

    public static IOrderedQueryable<T> ApplySortOrder<T>(this IQueryable<T> query, string sortOrder, IQueryKitConfiguration config)
        => Apply(SortOrderParameter, () =>
        {
            foreach (var sort in SortParser.ParseSort<T>(sortOrder, config))
                RefuseVersion(SortOrderParameter, sort.Expression);
            return query.ApplyQueryKitSort(sortOrder, config);
        });

    // A version is opaque, and Postgres has no order for its xid, so a range or a sort fails with 500 when the query runs.
    // PreventFilter and PreventSort of QueryKit drop the clause and give 200 with the wrong rows, so this check gives 400.
    private static void RefuseVersion(string parameter, Expression? expression)
    {
        if (expression is not null && VersionReader.Reads(expression))
            throw new QueryInputException(parameter, $"The {parameter} value cannot use version. A version is opaque.");
    }

    private static TResult Apply<TResult>(string parameter, Func<TResult> apply)
    {
        try
        {
            return apply();
        }
        // The messages of these types name only the input of the caller, so the response can show them
        catch (QueryKitException exception) when (IsInputError(exception))
        {
            throw new QueryInputException(parameter, exception.Message, exception);
        }
        // An operator that does not fit the type of the property fails in .NET, for example ^^ on a string.
        // The message names .NET types, so the response gets a fixed message and the trace gets the exception.
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Activity.Current?.AddException(exception);
            throw new QueryInputException(parameter, $"The {parameter} value is not valid.", exception);
        }
    }

    private sealed class VersionReader : ExpressionVisitor
    {
        private bool _reads;

        public static bool Reads(Expression expression)
        {
            var reader = new VersionReader();
            reader.Visit(expression);
            return reader._reads;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            _reads |= node.Member.Name == nameof(BaseEntity.Version);
            return base.VisitMember(node);
        }
    }

    // QueryKitDbContextTypeException and SoundsLikeNotImplementedException come from the setup of the Server, so they stay 500
    private static bool IsInputError(QueryKitException exception) => exception
        is QueryKitParsingException
        or ParsingException
        or SortParsingException
        or UnknownFilterPropertyException
        or QueryKitInputLengthExceededException
        or QueryKitNestingDepthExceededException
        or QueryKitPropertyDepthExceededException;
}
