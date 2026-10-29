namespace FullstackTemplate.Server.Domain.Agents.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Resources;
using Serilog;
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
        private const string TenantIdField = "tenantId";
        private const string ClientIdField = "clientId";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<AgentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // The requested tenant first, so that a refused request also has it on the span
            Activity.Current?.SetTag("app.tenant_id", request.Dto.TenantId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageAgents());
            if (AgentMode.Of(request.Dto.Mode).IsMachine)
                caller.RequireManagesAllTenants();
            caller.Require((request.Dto.Permissions ?? []).Select((name, i) => Permission.Of(name, $"permissions[{i}]")));

            var tenantId = ResolveTenantId(request.Dto.TenantId);
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            // The shared lock keeps a tenant delete out until the save, so the agent does not land in a deleted tenant.
            var added = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
            {
                var tenantExists = await dbContext.Tenants
                    .TagWithOperationCallSite("agents.add.tenant_exists")
                    .AnyAsync(t => t.Id == tenantId, ct);
                if (!tenantExists)
                    throw new ValidationException(TenantIdField, "The tenant does not exist.");

                var agent = Agent.Create(request.Dto.ToAgentForCreation(tenantId), caller);
                Activity.Current?.SetTag("app.agent_id", agent.Id);
                Activity.Current?.SetTag("app.agent.mode", agent.Mode.Value);

                if (firstPartyClients.Value.Contains(agent.ClientId))
                    throw new ValidationException(ClientIdField,
                        "This is the client of the app. Its users act with their own permissions, so it cannot be an agent.");
                var tenancy = tenancyOptions.Value;
                if (tenancy.ManageAllTenantsClientIds.Contains(agent.ClientId, StringComparer.Ordinal)
                    || tenancy.ReadAllTenantsClientIds.Contains(agent.ClientId, StringComparer.Ordinal))
                    throw new ValidationException(ClientIdField,
                        "This client acts for all tenants, so it cannot be an agent of one tenant.");

                var clientIdInUse = await dbContext.Agents
                    .TagWithOperationCallSite("agents.add.client_id_in_use")
                    .IgnoreQueryFilters([QueryFilterNames.Tenant])
                    .AnyAsync(a => a.ClientId == agent.ClientId
                        && (a.TenantId == agent.TenantId || a.Mode.Value == AgentMode.MachineValue), ct);
                if (clientIdInUse)
                    throw new ConflictException(ClientIdField, Agent.ClientIdInUseMessage);

                await dbContext.Agents.AddAsync(agent, ct);
                await dbContext.SaveChangesAsync(ct);
                return agent;
            }, cancellationToken);

            _logger.Here().Information("Added agent {AgentId} to tenant {TenantId} in mode {Mode}", added.Id, added.TenantId, added.Mode.Value);

            return added.ToAgentDto();
        }

        private string ResolveTenantId(string? requestedTenantId)
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

            if (!EntityId.IsValid(requestedTenantId, Tenants.Tenant.IdPrefix))
                throw new ValidationException(TenantIdField, "The tenant does not exist.");

            return requestedTenantId;
        }
    }
}
