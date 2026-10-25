namespace FullstackTemplate.Server.Domain.Agents.Features;

using System.Diagnostics;
using Databases;
using MediatR;
using Resources;
using Serilog;
using Users;

public static class DeleteAgent
{
    public sealed record Command(string Id) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard) : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.agent_id", request.Id);

            await permissionGuard.RequireAsync(Permission.ManageAgents(), cancellationToken);

            var agent = await dbContext.Agents
                .TagWithOperationCallSite("agents.delete.load")
                .GetById(request.Id, cancellationToken);

            foreach (var permission in agent.Permissions.ToList())
            {
                dbContext.AgentPermissions.Remove(permission);
            }

            dbContext.Agents.Remove(agent);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Deleted agent {AgentId}", agent.Id);
        }
    }
}
