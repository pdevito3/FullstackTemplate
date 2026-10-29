namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
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
public static partial class QueryKitInputExtensions
{
    public const string FiltersParameter = "filters";
    public const string SortOrderParameter = "sortOrder";
    public const string FilterNotReadMessage = "The filter could not be read. Change the filter, then try again.";
    public const string SortOrderNotReadMessage = "The sort order could not be read. Change the sort, then try again.";
    public const string FilterTooLongMessage = "The filter is too long. Remove a condition or shorten a value, then try again.";
    public const string FilterCharacterMessage = "The filter has a character that is not allowed. Remove it, then try again.";
    public const string FilterTooDeepMessage = "The filter has too many nested groups. Remove a group, then try again.";
    public const string SoundsLikeMessage =
        "This list cannot filter with the sounds-like operators ~~ and !~. Choose another operator, then try again.";
    // The convention name of the allowed actions on the read DTO of each entity, so this check covers each list.
    private const string AllowedActionsMember = "AllowedActions";

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, string filters, IQueryKitConfiguration config)
        where T : class
        => Apply<T, IQueryable<T>>(FiltersParameter, () =>
        {
            // Filter values go to Postgres as parameters, and Postgres refuses a parameter that it cannot store
            if (!PostgresText.CanStore(filters))
                throw new QueryInputException(FiltersParameter, FilterCharacterMessage);
            var predicate = FilterParser.ParseFilter<T>(filters, config);
            RefuseFieldsOutsideTheQuery(FiltersParameter, predicate);
            return query.Where(predicate);
        });

    public static IOrderedQueryable<T> ApplySortOrder<T>(this IQueryable<T> query, string sortOrder, IQueryKitConfiguration config)
        => Apply<T, IOrderedQueryable<T>>(SortOrderParameter, () =>
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

    private static TResult Apply<T, TResult>(string parameter, Func<TResult> apply)
    {
        try
        {
            return apply();
        }
        // Each QueryKit error comes from the filter or the sort of the caller, so it is a 400 that tells the caller what to change
        catch (QueryKitException exception)
        {
            AddToSpan(exception);
            throw new QueryInputException(parameter, MessageFor<T>(parameter, exception), exception);
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

    // The QueryKit messages name the field but its exceptions do not, so the name comes from the message.
    // A message that does not match gets the fixed message.
    private static string MessageFor<T>(string parameter, QueryKitException exception) => exception switch
    {
        QueryKitInputLengthExceededException => FilterTooLongMessage,
        QueryKitNestingDepthExceededException => FilterTooDeepMessage,
        QueryKitDbContextTypeException or SoundsLikeNotImplementedException => SoundsLikeMessage,
        UnknownFilterPropertyException when UnknownFilterField().Match(exception.Message) is { Success: true } match =>
            $"The filter names the field {match.Groups[1].Value}, which this list does not have. Use one of these fields: {FieldsOf<T>()}.",
        SortParsingException when UnknownSortField().Match(exception.Message) is { Success: true } match =>
            $"The sort order names the field {match.Groups[1].Value}, which this list does not have. Use one of these fields: {FieldsOf<T>()}.",
        { InnerException: ParseException parse } =>
            $"The {Words(parameter)} could not be read at character {parse.Position.Pos + 1}. Check the operator and the quotes there, then try again.",
        ParsingException when ValueDoesNotFit().Match(exception.Message) is { Success: true } match =>
            $"A value in the filter does not fit the field {match.Groups[1].Value}. Change the value, then try again.",
        _ => NotReadMessage(parameter),
    };

    private static string FieldsOf<T>() => string.Join(", ", RequestRules.For(typeof(T))
        .Where(rule => rule.Holds == PropertyHolds.Value
            && rule.Property.Name is not (nameof(BaseEntity.Version) or AllowedActionsMember))
        .Select(rule => rule.JsonName));

    [GeneratedRegex(@"^The filter property '(.+)' was not recognized\.$")]
    private static partial Regex UnknownFilterField();

    [GeneratedRegex(@"^Parsing failed during sorting\. '(.+)' was not recognized\.$")]
    private static partial Regex UnknownSortField();

    [GeneratedRegex(@" for the filter property '(.+)'\.$")]
    private static partial Regex ValueDoesNotFit();

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
}
