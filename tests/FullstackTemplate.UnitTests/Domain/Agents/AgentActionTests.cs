namespace FullstackTemplate.UnitTests.Domain.Agents;

using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Mappings;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class AgentActionTests
{
    private const string ManageAgentsDenial = "You cannot manage agents. Ask a tenant admin for access.";
    private const string ReadAgentsDenial = "You cannot see the agents of the tenant. Ask a tenant admin for access.";

    private static readonly AgentAction[] Actions =
        [AgentAction.AddPermission(), AgentAction.RemovePermission(), AgentAction.Delete()];

    private static Caller.Member MemberWith(params Permission[] permissions) => new("usr_caller", permissions);

    private static readonly Caller[] Callers =
    [
        Caller.AllTenantsManager.Instance,
        new Caller.Outsider(OutsiderKind.Anonymous),
        MemberWith(),
        MemberWith(Permission.ReadAgents()),
        MemberWith(Permission.ManageAgents()),
        MemberWith(Permission.ManageAgents(), Permission.ReadAgents()),
        MemberWith([.. Permission.GetAll()]),
        new Caller.DelegatedAgent("clinic-assistant", Permission.GetAll(), [Permission.ReadAgents()]),
    ];

    public static IEnumerable<Func<(string Case, AgentAction Action, Caller Caller, string? Denial)>> Cases()
    {
        yield return () => ("member with no permission", AgentAction.AddPermission(), MemberWith(), ManageAgentsDenial);
        yield return () => ("reader", AgentAction.AddPermission(), MemberWith(Permission.ReadAgents()), ManageAgentsDenial);
        yield return () => ("manager without read_agents", AgentAction.AddPermission(),
            MemberWith(Permission.ManageAgents()), ReadAgentsDenial);
        yield return () => ("manager with read_agents", AgentAction.AddPermission(),
            MemberWith(Permission.ManageAgents(), Permission.ReadAgents()), null);

        yield return () => ("reader", AgentAction.RemovePermission(), MemberWith(Permission.ReadAgents()), ManageAgentsDenial);
        yield return () => ("manager without read_agents", AgentAction.RemovePermission(),
            MemberWith(Permission.ManageAgents()), ReadAgentsDenial);
        yield return () => ("manager with read_agents", AgentAction.RemovePermission(),
            MemberWith(Permission.ManageAgents(), Permission.ReadAgents()), null);

        yield return () => ("reader", AgentAction.Delete(), MemberWith(Permission.ReadAgents()), ManageAgentsDenial);
        yield return () => ("manager without read_agents", AgentAction.Delete(), MemberWith(Permission.ManageAgents()), null);
        yield return () => ("anonymous", AgentAction.Delete(), new Caller.Outsider(OutsiderKind.Anonymous), "Sign in to do this.");
        yield return () => ("delegated agent whose grant lacks manage_agents", AgentAction.Delete(),
            new Caller.DelegatedAgent("clinic-assistant", Permission.GetAll(), [Permission.ReadAgents()]),
            "This agent has no grant to manage agents. A tenant admin can grant it.");
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    public void an_action_is_allowed_only_when_its_guard_passes(
        string @case, AgentAction action, Caller caller, string? denial)
    {
        // Arrange
        var agent = AnAgent();

        // Act
        var allowedActions = agent.ToAgentDto(caller).AllowedActions;
        var act = () => action.GuardCaller(caller);

        // Assert
        if (denial is null)
        {
            act.ShouldNotThrow();
            allowedActions.ShouldContain(action.Value);
        }
        else
        {
            act.ShouldThrow<ForbiddenAccessException>().Message.ShouldBe(denial);
            allowedActions.ShouldNotContain(action.Value);
        }
    }

    [Test]
    public void the_list_and_the_guards_agree_for_each_caller_and_action()
    {
        // Arrange
        var agent = AnAgent();

        foreach (var caller in Callers)
        {
            // Act
            var allowedActions = agent.ToAgentDto(caller).AllowedActions;

            // Assert
            foreach (var action in Actions)
                allowedActions.Contains(action.Value).ShouldBe(Passes(() => action.GuardCaller(caller)), $"{action} for {caller}");
        }
    }

    [Test]
    public void a_row_of_the_list_query_gets_the_actions_of_the_record()
    {
        // Arrange
        var agent = AnAgent();

        foreach (var caller in Callers)
        {
            var rows = new[] { agent }.AsQueryable().ToAgentDtoWithoutActionsQueryable().ToList();

            // Act
            rows.AddAllowedActions(caller);

            // Assert
            rows.Single().AllowedActions.ShouldBe(agent.ToAgentDto(caller).AllowedActions, $"for {caller}");
        }
    }

    [Test]
    public void the_list_keeps_the_order_of_the_table()
    {
        // Arrange
        var agent = AnAgent();

        // Act
        var allowedActions = agent.ToAgentDto(Caller.AllTenantsManager.Instance).AllowedActions;

        // Assert
        allowedActions.ShouldBe(["AddPermission", "RemovePermission", "Delete"]);
    }

    private static Agent AnAgent()
    {
        TestGraph.Start(out _).AddTenant(out _).AddAgent(out var agent);
        return agent;
    }

    private static bool Passes(Action act)
    {
        try
        {
            act();
            return true;
        }
        catch (ForbiddenAccessException)
        {
            return false;
        }
    }
}
