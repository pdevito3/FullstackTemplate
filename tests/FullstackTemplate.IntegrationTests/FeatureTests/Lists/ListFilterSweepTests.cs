namespace FullstackTemplate.IntegrationTests.FeatureTests.Lists;

using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Examples.Venues.Dtos;
using FullstackTemplate.Server.Examples.Venues.Features;
using FullstackTemplate.Server.Examples.Workshops.Dtos;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>
/// Each operator of QueryKit on each field of each list, with null, one value, and a list. The list runs the query, or it
/// refuses the value with a <see cref="QueryInputException"/>, which the endpoints give as 400. Any other exception is a
/// 500 on HTTP, an "An error occurred" result on MCP, and UNKNOWN on gRPC.
/// </summary>
public class ListFilterSweepTests : TestBase
{
    private static readonly string[] Comparisons =
        ["==", "!=", ">", "<", ">=", "<=", "@=", "_=", "_-=", "!@=", "!_=", "!_-=", "^$", "!^$"];
    private static readonly string[] InOperators = ["^^", "!^^"];
    private static readonly string[] CountOperators = ["#==", "#!=", "#>", "#<", "#>=", "#<="];

    private static IEnumerable<string> Operators() =>
        Comparisons.Concat(InOperators).Concat(CountOperators)
            .SelectMany(op => new[] { op, $"{op}*" })
            .Concat(InOperators.Concat(["^$", "!^$"]).Select(op => $"%{op}"));

    public static IEnumerable<string> Lists => ["workshops", "venues", "users", "agents", "tenants"];

    [Test]
    [MethodDataSource(nameof(Lists))]
    public async Task each_operator_on_each_field_runs_or_gives_a_query_input_error(string list)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var (dto, send) = ListOf(list, testingServiceScope);
        var faults = new List<string>();

        // Act
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

        // Assert
        faults.ShouldBeEmpty();
    }

    // QueryKit reads a member of a field, also a private one. EF cannot translate most of them.
    [Test]
    [Arguments("startsAt.ticks > 5", null)]
    [Arguments("startsAt.utcTicks > 5", null)]
    [Arguments("startsAt.totalOffsetMinutes > 3", null)]
    [Arguments("startsAt.dateTime > 2030-01-01T00:00:00Z", null)]
    [Arguments("capacity.m_value > 3", null)]
    [Arguments("title.length > 3", null)]
    [Arguments(null, "startsAt.ticks desc")]
    [Arguments(null, "startsAt.offset")]
    public async Task a_part_of_a_field_gives_a_query_input_error(string? filters, string? sortOrder)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetWorkshopList.Query(
            WorkshopParametersDto.From(null, null, filters, sortOrder, null)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe(filters is null ? "sortOrder" : "filters");
    }

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

    private static (Type Dto, Func<string?, string?, Task> Send) ListOf(string list, TestingServiceScope scope) => list switch
    {
        "workshops" => (typeof(WorkshopDto), (filters, sort) =>
            scope.SendAsync(new GetWorkshopList.Query(WorkshopParametersDto.From(null, null, filters, sort, null)))),
        "venues" => (typeof(VenueDto), (filters, sort) =>
            scope.SendAsync(new GetVenueList.Query(VenueParametersDto.From(null, null, filters, sort, null)))),
        "users" => (typeof(UserDto), (filters, sort) =>
            scope.SendAsync(new GetUserList.Query(UserParametersDto.From(null, null, filters, sort)))),
        "agents" => (typeof(AgentDto), (filters, sort) =>
            scope.SendAsync(new GetAgentList.Query(AgentParametersDto.From(null, null, filters, sort)))),
        "tenants" => (typeof(TenantDto), (filters, sort) =>
            scope.SendAsync(new GetTenantList.Query(TenantParametersDto.From(null, null, filters, sort)))),
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };
}
