namespace FullstackTemplate.IntegrationTests.FeatureTests.Lists;

using FullstackTemplate.Server.Exceptions;

/// <summary>
/// Each operator of QueryKit on each field of a list, with null, one value, and a list, and a sort on each field. The
/// list runs the query, or it refuses the value with a <see cref="QueryInputException"/>, which the endpoints give as
/// 400. Any other exception is a 500 on HTTP, an "An error occurred" result on MCP, and UNKNOWN on gRPC.
/// </summary>
public static class ListFilterSweep
{
    private static readonly string[] Comparisons =
        ["==", "!=", ">", "<", ">=", "<=", "@=", "_=", "_-=", "!@=", "!_=", "!_-=", "^$", "!^$"];
    private static readonly string[] InOperators = ["^^", "!^^"];
    private static readonly string[] CountOperators = ["#==", "#!=", "#>", "#<", "#>=", "#<="];

    /// <summary>Gives each input that threw an exception other than a query input error, with its type.</summary>
    /// <param name="dto">The read DTO of the list. Its properties are the fields.</param>
    /// <param name="send">Sends the list query with a filter and a sort order.</param>
    public static async Task<IReadOnlyList<string>> FaultsAsync(Type dto, Func<string?, string?, Task> send)
    {
        var faults = new List<string>();
        // A type scan in a test: it reads the fields of the contract, so a new field gets the sweep too
        foreach (var property in dto.GetProperties())
        {
            var field = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            var value = SampleOf(property.PropertyType);
            foreach (var op in Operators())
                foreach (var right in new[] { "null", value, $"[{value}, {value}]" })
                    await Record(faults, $"{field} {op} {right}", () => send($"{field} {op} {right}", null));
            await Record(faults, $"sort {field}", () => send(null, field));
        }
        return faults;
    }

    private static IEnumerable<string> Operators() =>
        Comparisons.Concat(InOperators).Concat(CountOperators)
            .SelectMany(op => new[] { op, $"{op}*" })
            .Concat(InOperators.Concat(["^$", "!^$"]).Select(op => $"%{op}"));

    private static async Task Record(List<string> faults, string input, Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (QueryInputException)
        {
        }
        catch (Exception exception)
        {
            faults.Add($"{input}: {exception.GetType().Name}");
        }
    }

    private static string SampleOf(Type type) => type switch
    {
        _ when type == typeof(int) || type == typeof(uint) => "5",
        _ when type == typeof(DateTimeOffset) => "2030-01-01T00:00:00Z",
        _ => "\"x\"",
    };
}
