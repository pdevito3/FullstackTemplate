namespace FullstackTemplate.Server.Domain.Agents;

using DomainEvents;
using Exceptions;
using Models;
using Tenants;
using Users;

/// <summary>
/// An OAuth client that a tenant lets act in the tenant, and the permissions that the tenant grants it. The client
/// signs in at the identity provider. This record is the decision of the tenant about it. A tenant has at most one
/// live agent for each client ID. A client ID has at most one live machine agent in all tenants, because the token of
/// a machine names no tenant: the agent gives the tenant.
/// </summary>
/// <remarks>
/// <see cref="ClientId"/> and <see cref="Mode"/> do not change after creation, because a different trust is a new
/// agent. Each permission of the grant is one that the grantor held when it gave it.
/// </remarks>
public class Agent : BaseEntity, ITenantable
{
    public const string IdPrefix = "agt";
    public const int NameMaxLength = 200;
    public const int ClientIdMaxLength = 255;
    public const string ClientIdInUseMessage = "An agent with this client ID already exists.";

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string Name { get; private set; } = default!;

    /// <summary>The client ID in the tokens of the client. Case-sensitive.</summary>
    [NotSensitive] public string ClientId { get; private set; } = default!;

    [NotSensitive] public AgentMode Mode { get; private set; } = default!;

    private readonly List<AgentPermission> _permissions = [];

    /// <summary>The grant. It is the most that the agent can hold.</summary>
    public IReadOnlyCollection<AgentPermission> Permissions => _permissions.AsReadOnly();

    /// <summary>
    /// Creates the agent with its first grant. Only a caller that manages all tenants can create a machine agent, so a
    /// tenant cannot take the machine client ID of another tenant. Each permission of the first grant follows the rule
    /// of <see cref="Grant"/>.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">
    /// The grantor cannot create a machine agent, or does not hold a permission of the first grant.
    /// </exception>
    /// <exception cref="ValidationException">A field is empty or not valid.</exception>
    public static Agent Create(AgentForCreation forCreation, Caller grantor)
    {
        var mode = AgentMode.Of(forCreation.Mode);
        if (mode.IsMachine)
            grantor.RequireManagesAllTenants();

        var agent = new Agent
        {
            TenantId = forCreation.TenantId,
            Name = forCreation.Name,
            ClientId = forCreation.ClientId,
            Mode = mode
        };
        if (forCreation.Id is not null)
            agent.SetId(forCreation.Id);

        ValidateAgent(agent);
        for (var i = 0; i < forCreation.Permissions.Count; i++)
            agent.AddToGrant(Permission.Of(forCreation.Permissions[i], $"permissions[{i}]"), grantor);

        agent.QueueDomainEvent(new AgentCreated(agent.Id));

        return agent;
    }

    /// <summary>
    /// Adds the permission to the grant. The grantor must hold it, so a grantor cannot make an agent stronger than the
    /// grantor. A second grant of the same permission changes nothing.
    /// </summary>
    /// <exception cref="ForbiddenAccessException">The grantor does not hold the permission.</exception>
    public Agent Grant(Permission permission, Caller grantor)
    {
        if (AddToGrant(permission, grantor))
            QueueDomainEvent(new AgentUpdated(Id));

        return this;
    }

    /// <summary>
    /// Takes the permission out of the grant. Unlike <see cref="Grant"/>, it needs no grantor who holds the
    /// permission: a smaller grant cannot raise anyone's access, and a tenant admin must be able to contain an agent
    /// that holds more than the admin. A permission that is not in the grant changes nothing.
    /// </summary>
    public Agent Revoke(Permission permission)
    {
        if (_permissions.RemoveAll(p => p.Permission == permission) > 0)
            QueueDomainEvent(new AgentUpdated(Id));

        return this;
    }

    private bool AddToGrant(Permission permission, Caller grantor)
    {
        grantor.Require(permission);
        if (_permissions.Any(p => p.Permission == permission))
            return false;

        _permissions.Add(AgentPermission.Create(this, permission));
        return true;
    }

    private static void ValidateAgent(Agent agent)
    {
        ValidationException.ThrowWhenNullOrWhitespace(agent.TenantId, "tenantId", "Please provide a tenant.");
        ValidationException.Must(EntityId.IsValid(agent.TenantId, Tenant.IdPrefix), "tenantId", "Please provide a valid tenant ID.");
        ValidationException.ThrowWhenNullOrWhitespace(agent.Name, "name", "Please provide a name.");
        ValidationException.ThrowWhenLongerThan(agent.Name, NameMaxLength, "name",
            $"A name can have no more than {NameMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(agent.Name, "name",
            "A name has a character that is not allowed.");
        ValidationException.ThrowWhenNullOrWhitespace(agent.ClientId, "clientId", "Please provide a client ID.");
        ValidationException.ThrowWhenLongerThan(agent.ClientId, ClientIdMaxLength, "clientId",
            $"A client ID can have no more than {ClientIdMaxLength} characters.");
        ValidationException.ThrowWhenHasCharacterPostgresCannotStore(agent.ClientId, "clientId",
            "A client ID has a character that is not allowed.");
        ValidationException.Must(agent.ClientId == agent.ClientId.Trim(), "clientId",
            "A client ID cannot start or end with a space.");
    }

    protected Agent() : base(IdPrefix) { } // For EF Core
}
