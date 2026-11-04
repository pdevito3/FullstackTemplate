namespace FullstackTemplate.Server.Domain.Invites.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Users;

public static class GetInvitePreview
{
    public sealed record Query(InviteTokenDto Dto) : IRequest<InvitePreviewDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        ICallerIdentityResolver callerIdentityResolver,
        TimeProvider timeProvider) : IRequestHandler<Query, InvitePreviewDto>
    {
        public async Task<InvitePreviewDto> Handle(Query request, CancellationToken cancellationToken)
        {
            InviteCaller.RequireUser(await callerIdentityResolver.GetAsync(cancellationToken));

            // The token is the access: the caller can be in no tenant or in another tenant, so the tenant filter is off.
            var hash = InviteToken.HashOf(request.Dto.Token);
            var found = await dbContext.Invites
                .TagWithOperationCallSite("invites.preview.load")
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .AsNoTracking()
                .Where(i => i.TokenHash == hash)
                .Join(dbContext.Tenants.IgnoreQueryFilters([QueryFilterNames.Tenant]),
                    i => i.TenantId, t => t.Id, (i, t) => new { Invite = i, TenantName = t.Name })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(Invite.NotValidMessage);
            Activity.Current?.SetTag("app.invite_id", found.Invite.Id);
            Activity.Current?.SetTag("app.tenant_id", found.Invite.TenantId);

            found.Invite.GuardClaimable(timeProvider.GetUtcNow());

            return new InvitePreviewDto
            {
                TenantName = found.TenantName,
                Role = found.Invite.Role.Value,
                ExpiresAt = found.Invite.ExpiresAt,
                RequireEmailMatch = found.Invite.RequireEmailMatch,
            };
        }
    }
}
