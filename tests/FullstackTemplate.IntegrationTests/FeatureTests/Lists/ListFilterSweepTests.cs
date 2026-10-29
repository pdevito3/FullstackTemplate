namespace FullstackTemplate.IntegrationTests.FeatureTests.Lists;

using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using Shouldly;

/// <summary>The sweep of <see cref="ListFilterSweep"/> on each list of the Server.</summary>
public class ListFilterSweepTests : TestBase
{
    public static IEnumerable<string> Lists => ["users", "agents", "tenants"];

    [Test]
    [MethodDataSource(nameof(Lists))]
    public async Task each_operator_on_each_field_runs_or_gives_a_query_input_error(string list)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var (dto, send) = ListOf(list, testingServiceScope);

        // Act
        var faults = await ListFilterSweep.FaultsAsync(dto, send);

        // Assert
        faults.ShouldBeEmpty();
    }

    // QueryKit reads a member of a field, also a private one. EF cannot translate most of them.
    [Test]
    [Arguments("firstName.length > 3", null)]
    [Arguments("version.m_value > 3", null)]
    [Arguments(null, "firstName.length desc")]
    public async Task a_part_of_a_field_gives_a_query_input_error(string? filters, string? sortOrder)
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();

        // Act
        var act = () => testingServiceScope.SendAsync(new GetUserList.Query(UserParametersDto.From(null, null, filters, sortOrder)));

        // Assert
        (await act.ShouldThrowAsync<QueryInputException>()).Parameter.ShouldBe(filters is null ? "sortOrder" : "filters");
    }

    private static (Type Dto, Func<string?, string?, Task> Send) ListOf(string list, TestingServiceScope scope) => list switch
    {
        "users" => (typeof(UserDto), (filters, sort) =>
            scope.SendAsync(new GetUserList.Query(UserParametersDto.From(null, null, filters, sort)))),
        "agents" => (typeof(AgentDto), (filters, sort) =>
            scope.SendAsync(new GetAgentList.Query(AgentParametersDto.From(null, null, filters, sort)))),
        "tenants" => (typeof(TenantDto), (filters, sort) =>
            scope.SendAsync(new GetTenantList.Query(TenantParametersDto.From(null, null, filters, sort)))),
        _ => throw new ArgumentOutOfRangeException(nameof(list), list, null),
    };
}
