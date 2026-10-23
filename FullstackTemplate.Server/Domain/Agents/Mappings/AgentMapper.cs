namespace FullstackTemplate.Server.Domain.Agents.Mappings;

using System.Diagnostics.CodeAnalysis;
using Dtos;
using Models;
using Riok.Mapperly.Abstractions;

[Mapper]
public static partial class AgentMapper
{
    [MapperIgnoreSource(nameof(Agent.CreatedBy))]
    [MapperIgnoreSource(nameof(Agent.LastModifiedBy))]
    [MapperIgnoreSource(nameof(Agent.CreatedByClient))]
    [MapperIgnoreSource(nameof(Agent.LastModifiedByClient))]
    [MapperIgnoreSource(nameof(Agent.CreatedOn))]
    [MapperIgnoreSource(nameof(Agent.LastModifiedOn))]
    [MapperIgnoreSource(nameof(Agent.IsDeleted))]
    [MapperIgnoreSource(nameof(Agent.DomainEvents))]
    public static partial AgentDto ToAgentDto(this Agent agent);

    public static partial IQueryable<AgentDto> ToAgentDtoQueryable(this IQueryable<Agent> queryable);

    [MapperIgnoreSource(nameof(AgentForCreationDto.TenantId))]
    [MapperIgnoreTarget(nameof(AgentForCreation.Id))]
    public static partial AgentForCreation ToAgentForCreation(this AgentForCreationDto dto, string tenantId);

    private static string MapMode(AgentMode mode) => mode.Value;
    private static IReadOnlyList<string> MapFirstGrant(IReadOnlyList<string>? permissions) => permissions ?? [];
    [SuppressMessage("Performance", "CA1859", Justification = "Mapperly matches on the exact target type.")]
    private static IReadOnlyList<string> MapPermissions(IReadOnlyCollection<AgentPermission> permissions)
        => permissions.Select(p => p.Permission.Value).ToList();
}
