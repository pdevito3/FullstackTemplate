namespace FullstackTemplate.Server.Domain.Agents.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Resources;
using Services;
using Users;

public static class AddAgent
{
    public sealed record Command(AgentForCreationDto Dto) : IRequest<AgentDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        IPermissionGuard permissionGuard,
        IOptions<FirstPartyClientOptions> firstPartyClients,
        IOptions<TenancyOptions> tenancyOptions) : IRequestHandler<Command, AgentDto>
    {
        // The key in the errors of the problem is the name of the request field
        private const string TenantIdField = "tenantId";
        private const string ClientIdField = "clientId";

        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());

            var tenantId = await ResolveTenantIdAsync(request.Dto.TenantId, cancellationToken);
            var agent = Agent.Create(request.Dto.ToAgentForCreation(tenantId), caller);

            // CallerIdentityResolver reads these clients before it looks for an agent, so such an agent could never act.
            if (firstPartyClients.Value.Contains(agent.ClientId))
                throw new ValidationException(ClientIdField,
                    "This is the client of the app. Its users act with their own permissions, so it cannot be an agent.");
            var tenancy = tenancyOptions.Value;
            if (tenancy.ManageAllTenantsClientIds.Contains(agent.ClientId, StringComparer.Ordinal)
                || tenancy.ReadAllTenantsClientIds.Contains(agent.ClientId, StringComparer.Ordinal))
                throw new ValidationException(ClientIdField,
                    "This client acts for all tenants, so it cannot be an agent of one tenant.");

            // A live machine agent in any tenant takes the client from each delegated agent, because the resolver
            // reads the machine binding first. So a client ID with a machine agent cannot get a second agent anywhere.
            // The tenant filter does not apply, so that the check sees the other tenants. The soft delete filter
            // applies, because a deleted agent frees its client ID. The unique indexes stop a duplicate that passes
            // this check at the same time.
            var clientIdInUse = await dbContext.Agents
                .TagWithOperationCallSite("agents.add.client_id_in_use")
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .AnyAsync(a => a.ClientId == agent.ClientId
                    && (a.TenantId == agent.TenantId || a.Mode.Value == AgentMode.MachineValue), cancellationToken);
            if (clientIdInUse)
                throw new ConflictException(ClientIdField, "An agent with this client ID already exists.");

            await dbContext.Agents.AddAsync(agent, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            return agent.ToAgentDto();
        }

        // A tenant admin adds to their own tenant. A caller that sees all tenants has no tenant of its own,
        // so it must name the tenant.
        private async Task<string> ResolveTenantIdAsync(string? requestedTenantId, CancellationToken cancellationToken)
        {
            var scope = dbContext.CurrentTenantScope;
            if (string.IsNullOrWhiteSpace(requestedTenantId))
            {
                if (scope.TenantId is { } ownTenantId)
                    return ownTenantId;
                if (scope.IncludesAllTenants)
                    throw new ValidationException(TenantIdField, "Please provide a tenant.");
                throw new ForbiddenAccessException("You can add an agent only to your own tenant.");
            }

            if (!scope.Includes(requestedTenantId))
                throw new ForbiddenAccessException("You can add an agent only to your own tenant.");

            // The tenant filter and the soft delete filter apply, so a deleted tenant does not exist here.
            var tenantExists = await dbContext.Tenants
                .TagWithOperationCallSite("agents.add.tenant_exists")
                .AnyAsync(t => t.Id == requestedTenantId, cancellationToken);
            if (!tenantExists)
                throw new ValidationException(TenantIdField, "The tenant does not exist.");

            return requestedTenantId;
        }
    }
}
