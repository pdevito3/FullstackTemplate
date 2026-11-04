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
/// A value that QueryKit cannot apply, or that uses the version, the allowed actions, or the expired flag, throws a <see cref="QueryInputException"/> with the name of its parameter.
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
    public const string SoundsLikeMessage =
        "This list cannot filter with the sounds-like operators ~~ and !~. Choose another operator, then try again.";
    public const string InOperatorListMessage =
        """The operators ^^ and !^^ need a list of values, for example ["a", "b"]. Put the values in brackets, then try again.""";
    public const string NullValueMessage = "Only the operators == and != can compare with null. Change the operator, then try again.";
    public const string ListFieldNullMessage =
        "A field that holds a list cannot compare with null. To find an empty list, use #== 0, then try again.";
    // The convention name of the allowed actions on the read DTO of each entity, so this check covers each list.
    private const string AllowedActionsMember = "AllowedActions";
    // The expired flag of an invite depends on the time, and the handler adds it after the page loads.
    private const string ExpiredMember = "IsExpired";

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, string filters, IQueryKitConfiguration config)
        where T : class
        => Apply<T, IQueryable<T>>(FiltersParameter, () =>
        {
            // Filter values go to Postgres as parameters, and Postgres refuses a parameter that it cannot store
            if (!PostgresText.CanStore(filters))
                throw new QueryInputException(FiltersParameter, FilterCharacterMessage);
            var predicate = FilterParser.ParseFilter<T>(filters, config);
            RefuseFieldsOutsideTheQuery(FiltersParameter, predicate);
            RefusePartsOfFields<T>(FiltersParameter, predicate);
            RefuseNullThatTheQueryCannotRun(predicate);
            return query.Where(predicate);
        });

    public static IOrderedQueryable<T> ApplySortOrder<T>(this IQueryable<T> query, string sortOrder, IQueryKitConfiguration config)
        => Apply<T, IOrderedQueryable<T>>(SortOrderParameter, () =>
        {
            foreach (var sort in SortParser.ParseSort<T>(sortOrder, config))
            {
                RefuseFieldsOutsideTheQuery(SortOrderParameter, sort.Expression);
                RefusePartsOfFields<T>(SortOrderParameter, sort.Expression);
            }
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
        if (MemberReader.Reads(expression, ExpiredMember))
            throw new QueryInputException(
                parameter,
                $"The {Words(parameter)} cannot use the expired state, because it depends on the time. Choose another field.");
    }

    // QueryKit reads a member of a field, such as startsAt.ticks, also a private one. EF cannot translate most of them, and
    // the query fails with 500 when it runs. So a filter or a sort uses only the fields of the list.
    private static void RefusePartsOfFields<T>(string parameter, Expression? expression)
    {
        if (expression is not null && PartReader.Reads(expression))
            throw new QueryInputException(parameter,
                $"The {Words(parameter)} can use a field of the list, not a part of a field. Use one of these fields: {FieldsOf<T>()}.");
    }

    // QueryKit parses these filters, and the query fails with 500 when it runs:
    // - ^$* null and !^$* null on a list field call ToLower on the null. https://github.com/pdevito3/QueryKit/issues/212
    // - == null or != null on a list field compares the whole list, and EF cannot translate the compare of a projected
    //   list. https://github.com/pdevito3/QueryKit/issues/211
    private static void RefuseNullThatTheQueryCannotRun(Expression predicate)
    {
        var reader = new NullReader();
        reader.Visit(predicate);
        if (reader.CallsOnNull)
            throw new QueryInputException(FiltersParameter, NullValueMessage);
        if (reader.ComparesListWithNull)
            throw new QueryInputException(FiltersParameter, ListFieldNullMessage);
    }

    private static string Words(string parameter) => parameter == SortOrderParameter ? "sort order" : "filter";

    private static TResult Apply<T, TResult>(string parameter, Func<TResult> apply)
    {
        try
        {
            return apply();
        }
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

    private static string MessageFor<T>(string parameter, QueryKitException exception) => exception switch
    {
        QueryKitInputLengthExceededException => FilterTooLongMessage,
        QueryKitNestingDepthExceededException => FilterTooDeepMessage,
        QueryKitDbContextTypeException or SoundsLikeNotImplementedException => SoundsLikeMessage,
        UnknownFilterPropertyException unknown =>
            $"The filter names the field {unknown.PropertyName}, which this list does not have. Use one of these fields: {FieldsOf<T>()}.",
        // QueryKit also throws it for an empty clause and for a sort on a list field, so an empty name or a name that the list
        // has is another fault. https://github.com/pdevito3/QueryKit/issues/213
        SortParsingException sort when sort.PropertyName.Length > 0 && !HasField<T>(sort.PropertyName) =>
            $"The sort order names the field {sort.PropertyName}, which this list does not have. Use one of these fields: {FieldsOf<T>()}.",
        _ when OperatorRefusal(exception)?.Reason == QueryKitParsingReason.NeedsList => InOperatorListMessage,
        _ when OperatorRefusal(exception)?.Reason == QueryKitParsingReason.RefusesNull => NullValueMessage,
        { InnerException: ParseException parse } =>
            $"The {Words(parameter)} could not be read at character {parse.Position.Pos + 1}. Check the operator and the quotes there, then try again.",
        ParsingException { PropertyName: { } property } =>
            $"A value in the filter does not fit the field {property}. Change the value, then try again.",
        _ => NotReadMessage(parameter),
    };

    // An operator refuses its value with a QueryKitParsingException, and the parser can wrap it in a ParsingException
    private static QueryKitParsingException? OperatorRefusal(QueryKitException exception) =>
        exception as QueryKitParsingException ?? exception.InnerException as QueryKitParsingException;

    private static bool HasField<T>(string name) => RequestRules.For(typeof(T))
        .Any(rule => string.Equals(rule.JsonName, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(rule.Property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string FieldsOf<T>() => string.Join(", ", RequestRules.For(typeof(T))
        .Where(rule => rule.Holds == PropertyHolds.Value
            && rule.Property.Name is not (nameof(BaseEntity.Version) or AllowedActionsMember or ExpiredMember))
        .Select(rule => rule.JsonName));

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

    private sealed class PartReader : ExpressionVisitor
    {
        private bool _reads;

        public static bool Reads(Expression expression)
        {
            var reader = new PartReader();
            reader.Visit(expression);
            return reader._reads;
        }

        // Value and HasValue of a nullable field are the field itself
        protected override Expression VisitMember(MemberExpression node)
        {
            _reads |= node.Expression is MemberExpression owner
                && !(Nullable.GetUnderlyingType(owner.Type) is not null && node.Member.Name is "Value" or "HasValue");
            return base.VisitMember(node);
        }
    }

    private sealed class NullReader : ExpressionVisitor
    {
        public bool CallsOnNull { get; private set; }
        public bool ComparesListWithNull { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            CallsOnNull |= IsNull(node.Object);
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
                ComparesListWithNull |= (IsNull(node.Right) && IsList(node.Left.Type)) || (IsNull(node.Left) && IsList(node.Right.Type));
            return base.VisitBinary(node);
        }

        private static bool IsNull(Expression? expression) => expression switch
        {
            ConstantExpression { Value: null } => true,
            UnaryExpression { NodeType: ExpressionType.Convert, Operand: var operand } => IsNull(operand),
            _ => false,
        };

        private static bool IsList(Type type) => type != typeof(string) && type.IsAssignableTo(typeof(System.Collections.IEnumerable));
    }
}
