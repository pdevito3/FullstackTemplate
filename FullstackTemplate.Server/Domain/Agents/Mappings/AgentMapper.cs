namespace FullstackTemplate.Server.Domain.Agents.Mappings;

using System.Diagnostics.CodeAnalysis;
using Dtos;
using Models;
using Riok.Mapperly.Abstractions;
using Users;

[Mapper]
public static partial class AgentMapper
{
    /// <summary>The DTO with the actions that <paramref name="caller"/> can send for the agent.</summary>
    public static AgentDto ToAgentDto(this Agent agent, Caller caller)
        => agent.ToAgentDtoWithoutActions() with { AllowedActions = NamesOf(AgentAction.AllowedFor(caller)) };

    /// <summary>Gives each row of a list the actions that <paramref name="caller"/> can send for it.</summary>
    public static void AddAllowedActions(this List<AgentDto> agents, Caller caller)
    {
        var allowedActions = NamesOf(AgentAction.AllowedFor(caller));
        for (var i = 0; i < agents.Count; i++)
            agents[i] = agents[i] with { AllowedActions = allowedActions };
    }

    // It takes the configuration of ToAgentDtoWithoutActions.
    public static partial IQueryable<AgentDto> ToAgentDtoWithoutActionsQueryable(this IQueryable<Agent> queryable);

    [MapperIgnoreSource(nameof(AgentForCreationDto.TenantId))]
    [MapperIgnoreTarget(nameof(AgentForCreation.Id))]
    public static partial AgentForCreation ToAgentForCreation(this AgentForCreationDto dto, string tenantId);

    [MapperIgnoreSource(nameof(Agent.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Agent.IsDeleted))]
    [MapperIgnoreSource(nameof(Agent.DomainEvents))]
    [MapperIgnoreTarget(nameof(AgentDto.AllowedActions))]
    private static partial AgentDto ToAgentDtoWithoutActions(this Agent agent);

    private static List<string> NamesOf(IEnumerable<AgentAction> actions) => actions.Select(a => a.Value).ToList();
    private static string MapMode(AgentMode mode) => mode.Value;
    private static IReadOnlyList<string> MapFirstGrant(IReadOnlyList<string>? permissions) => permissions ?? [];
    [SuppressMessage("Performance", "CA1859", Justification = "Mapperly matches on the exact target type.")]
    private static IReadOnlyList<string> MapPermissions(IReadOnlyCollection<AgentPermission> permissions)
        => permissions.Select(p => p.Permission.Value).ToList();
}
