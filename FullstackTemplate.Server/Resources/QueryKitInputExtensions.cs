namespace FullstackTemplate.Server.Resources;

using System.Diagnostics;
using Exceptions;
using QueryKit;
using QueryKit.Configuration;
using QueryKit.Exceptions;

/// <summary>
/// Applies the <c>filters</c> and <c>sortOrder</c> values of a list request with QueryKit.
/// A value that QueryKit cannot apply throws a <see cref="QueryInputException"/> with the name of its parameter.
/// </summary>
public static class QueryKitInputExtensions
{
    public const string FiltersParameter = "filters";
    public const string SortOrderParameter = "sortOrder";

    public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, string filters, IQueryKitConfiguration config)
        where T : class
        => Apply(FiltersParameter, () => query.ApplyQueryKitFilter(filters, config));

    public static IOrderedQueryable<T> ApplySortOrder<T>(this IQueryable<T> query, string sortOrder, IQueryKitConfiguration config)
        => Apply(SortOrderParameter, () => query.ApplyQueryKitSort(sortOrder, config));

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
