namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources.Extensions;
using Microsoft.Extensions.Configuration;
using Shouldly;

public class ServerRoleTests
{
    [Test]
    [Arguments(null, ServerRole.All)]
    [Arguments("", ServerRole.All)]
    [Arguments("  ", ServerRole.All)]
    [Arguments("All", ServerRole.All)]
    [Arguments("api", ServerRole.Api)]
    [Arguments("Api", ServerRole.Api)]
    [Arguments(" WORKER ", ServerRole.Worker)]
    [Arguments("Worker", ServerRole.Worker)]
    public void a_name_in_any_case_or_no_value_gives_its_role(string? value, ServerRole role)
    {
        // Act & Assert
        ServerRoles.From(Settings(value)).ShouldBe(role);
    }

    [Test]
    [Arguments("workr")]
    [Arguments("1")]
    [Arguments("Both")]
    public void a_value_that_is_not_a_role_name_throws_a_message_that_names_the_roles(string value)
    {
        // Act
        Action act = () => ServerRoles.From(Settings(value));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe($"Server:Role is '{value}'. Use All, Api, or Worker.");
    }

    [Test]
    [Arguments(ServerRole.All, true, true)]
    [Arguments(ServerRole.Api, true, false)]
    [Arguments(ServerRole.Worker, false, true)]
    public void each_role_serves_requests_or_runs_jobs_or_both(ServerRole role, bool servesRequests, bool runsJobs)
    {
        // Act & Assert
        role.ShouldSatisfyAllConditions(
            r => r.ServesRequests().ShouldBe(servesRequests),
            r => r.RunsJobs().ShouldBe(runsJobs));
    }

    private static IConfiguration Settings(string? role) => new ConfigurationBuilder()
        .AddInMemoryCollection([new(ServerRoles.ConfigurationKey, role)])
        .Build();
}
