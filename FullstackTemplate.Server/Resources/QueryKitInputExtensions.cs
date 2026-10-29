namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using System.Linq.Expressions;
using Databases;
using Domain;
using Exceptions;
using QueryKit;
using QueryKit.Configuration;
using QueryKit.Exceptions;
using Sprache;

/// <summary>
/// Applies the <c>filters</c> and <c>sortOrder</c> values of a list request with QueryKit.
/// A value that QueryKit cannot apply, or that uses the version or the allowed actions, throws a <see cref="QueryInputException"/> with the name of its parameter.
/// </summary>
public static class QueryKitInputExtensions
{
    public const string FiltersParameter = "filters";
    public const string SortOrderParameter = "sortOrder";
    public const string FilterNotReadMessage = "The filter could not be read. Change the filter, then try again.";
    public const string SortOrderNotReadMessage = "The sort order could not be read. Change the sort, then try again.";
    public const string FilterTooLongMessage = "The filter is too long. Remove a condition or shorten a value, then try again.";
    public const string FilterCharacterMessage = "The filter has a character that is not allowed. Remove it, then try again.";
    public const string FilterTooDeepMessage = "The filter has too many nested groups. Remove a group, then try again.";
    // The convention name of the allowed actions on the read DTO of each entity, so this check covers each list.
    private const string AllowedActionsMember = "AllowedActions";

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, string filters, IQueryKitConfiguration config)
        where T : class
        => Apply(FiltersParameter, () =>
        {
            // Filter values go to Postgres as parameters, and Postgres refuses a parameter that it cannot store
            if (!PostgresText.CanStore(filters))
                throw new QueryInputException(FiltersParameter, FilterCharacterMessage);
            var predicate = FilterParser.ParseFilter<T>(filters, config);
            RefuseFieldsOutsideTheQuery(FiltersParameter, predicate);
            return query.Where(predicate);
        });

    public static IOrderedQueryable<T> ApplySortOrder<T>(this IQueryable<T> query, string sortOrder, IQueryKitConfiguration config)
        => Apply(SortOrderParameter, () =>
        {
            foreach (var sort in SortParser.ParseSort<T>(sortOrder, config))
                RefuseFieldsOutsideTheQuery(SortOrderParameter, sort.Expression);
            return query.ApplyQueryKitSort(sortOrder, config);
        });

    // A version is opaque, and Postgres has no order for its xid, so a range or a sort fails with 500 when the query runs.
    // The handler adds AllowedActions after the page loads, so EF cannot translate a clause on it, and the query gives 500.
    // PreventFilter and PreventSort of QueryKit drop the clause and give 200 with the wrong rows, so this check gives 400.
    private static void RefuseFieldsOutsideTheQuery(string parameter, Expression? expression)
    {
        if (expression is null)
            return;
        if (MemberReader.Reads(expression, nameof(BaseEntity.Version)))
            throw new QueryInputException(parameter, $"The {Words(parameter)} cannot use the version. Choose another field.");
        if (MemberReader.Reads(expression, AllowedActionsMember))
            throw new QueryInputException(parameter,
                $"The {Words(parameter)} cannot use the allowed actions, because they depend on the user. Choose another field.");
    }

    private static string Words(string parameter) => parameter == SortOrderParameter ? "sort order" : "filter";

    private static TResult Apply<TResult>(string parameter, Func<TResult> apply)
    {
        try
        {
            return apply();
        }
        // The message of QueryKit names the place of the fault and the property names of the code, so the response gets
        // a fixed message.
        catch (QueryKitException exception) when (IsInputError(exception))
        {
            AddToSpan(exception);
            throw new QueryInputException(parameter, MessageFor(parameter, exception), exception);
        }
        // An operator that does not fit the type of the property fails in .NET, for example ^^ on a string.
        // The message names .NET types, so the response gets a fixed message and the trace gets the exception.
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            AddToSpan(exception);
            throw new QueryInputException(parameter, NotReadMessage(parameter), exception);
        }
    }

    // A span never holds user-entered text, and the message of QueryKit can hold a value of the filter, as in "The value
    // 'x' is not a valid Int32". So the span gets only a message that holds no input: the place of a parse fault, or a
    // limit. The stack trace is that of the exception alone, because ToString() adds the messages of inner exceptions.
    private static void AddToSpan(Exception exception)
    {
        var message = exception switch
        {
            QueryKitInputLengthExceededException or QueryKitNestingDepthExceededException => exception.Message,
            { InnerException: ParseException parse } => $"Failed at {parse.Position}.",
            _ => exception.GetType().Name,
        };
        Activity.Current?.AddException(exception, new TagList
        {
            { "exception.message", message },
            { "exception.stacktrace", exception.StackTrace },
        });
    }

    private static string MessageFor(string parameter, QueryKitException exception) => exception switch
    {
        QueryKitInputLengthExceededException => FilterTooLongMessage,
        QueryKitNestingDepthExceededException => FilterTooDeepMessage,
        _ => NotReadMessage(parameter),
    };

    private static string NotReadMessage(string parameter) =>
        parameter == SortOrderParameter ? SortOrderNotReadMessage : FilterNotReadMessage;

    private sealed class MemberReader(string memberName) : ExpressionVisitor
    {
        private bool _reads;

        public static bool Reads(Expression expression, string memberName)
        {
            var reader = new MemberReader(memberName);
            reader.Visit(expression);
            return reader._reads;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            _reads |= node.Member.Name == memberName;
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
