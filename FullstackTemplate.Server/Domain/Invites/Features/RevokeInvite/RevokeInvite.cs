namespace FullstackTemplate.Server.Domain.Invites.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;
using Users;

public static class RevokeInvite
{
    public sealed record Command(string Id) : IRequest<InviteDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, InviteDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<InviteDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.invite_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            InviteAction.Revoke().GuardCaller(caller);

            var invite = await dbContext.Invites
                .TagWithOperationCallSite("invites.revoke.load")
                .GetById(request.Id, cancellationToken);

            var wasRevoked = invite.Status == InviteStatus.Revoked();
            invite.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!wasRevoked)
                _logger.Here().Information("Revoked invite {InviteId}", invite.Id);

            return invite.ToInviteDto(caller);
        }
    }
}
