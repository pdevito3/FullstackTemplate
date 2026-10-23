namespace FullstackTemplate.Server.Domain.Agents;

using Users;

/// <summary>One permission in the grant of an agent. It has the same shape as <see cref="UserPermission"/>.</summary>
public class AgentPermission : BaseEntity
{
    public const string IdPrefix = "aperm";

    public string AgentId { get; private set; } = default!;
    public Permission Permission { get; private set; } = default!;

    internal static AgentPermission Create(Agent agent, Permission permission)
    {
        return new AgentPermission
        {
            AgentId = agent.Id,
            Permission = permission
        };
    }

    protected AgentPermission() : base(IdPrefix) { } // EF Core
}
