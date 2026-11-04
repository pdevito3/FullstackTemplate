namespace FullstackTemplate.Server.Domain.Invites.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models;
using Resources;
using Serilog;
using Users;
using Users.Dtos;
using Users.Mappings;

public static class ClaimInvite
{
    // A claim that loses the race on the invite runs again, and then the invite is claimed, so the caller gets 410
    public sealed record Command(ClaimInviteDto Dto) : IRequest<UserDto>, IRunsAgainWhenRaced;

    public sealed class Handler(
        AppDbContext dbContext,
        ICallerIdentityResolver callerIdentityResolver,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider) : IRequestHandler<Command, UserDto>
    {
        private const string IdentifierIndex = "ix_users_identifier";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<UserDto> Handle(Command request, CancellationToken cancellationToken)
        {
            var caller = InviteCaller.RequireUser(await callerIdentityResolver.GetAsync(cancellationToken));
            // A user belongs to one tenant only (INV-6)
            if (caller.LiveUser is not null)
                throw new ConflictException(string.Empty, Invite.CallerHasTenantMessage);

            // The caller has no tenant yet, so the tenant filter is off. The token is the access.
            var hash = InviteToken.HashOf(request.Dto.Token);
            var tenantId = await dbContext.Invites
                .TagWithOperationCallSite("invites.claim.find_tenant")
                .IgnoreQueryFilters([QueryFilterNames.Tenant])
                .Where(i => i.TokenHash == hash)
                .Select(i => i.TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException(Invite.NotValidMessage);
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            // The shared lock keeps a tenant delete out until the save, so the user does not land in a deleted tenant.
            // A tenant delete deletes its invites, so an invite that loads here has a live tenant.
            var (invite, user) = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
            {
                var invite = await dbContext.Invites
                    .TagWithOperationCallSite("invites.claim.load")
                    .IgnoreQueryFilters([QueryFilterNames.Tenant])
                    .FirstOrDefaultAsync(i => i.TokenHash == hash && i.TenantId == tenantId, ct)
                    ?? throw new NotFoundException(Invite.NotValidMessage);
                Activity.Current?.SetTag("app.invite_id", invite.Id);

                var user = invite.Claim(new InviteClaim
                {
                    Identifier = caller.Subject,
                    TokenEmail = currentUserService.Email,
                    FirstName = request.Dto.FirstName,
                    LastName = request.Dto.LastName,
                    Email = request.Dto.Email,
                    Username = request.Dto.Username,
                }, timeProvider.GetUtcNow());
                Activity.Current?.SetTag("app.user_id", user.Id);

                // One save for the claim and the user, so the version check of the invite stops a second claim (INV-5)
                await dbContext.Users.AddAsync(user, ct);
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                // Another claim of the same sub saved first. The index covers the live users of all tenants.
                catch (ConstraintConflictException exception) when (exception.Constraint == IdentifierIndex)
                {
                    throw new ConflictException(string.Empty, Invite.CallerHasTenantMessage);
                }

                return (invite, user);
            }, cancellationToken);

            _logger.Here().Information("Claimed invite {InviteId} as new user {UserId} in tenant {TenantId} with role {Role}",
                invite.Id, user.Id, tenantId, user.Role.Value);
            InviteMetrics.Claimed.Add(1, new KeyValuePair<string, object?>("app.user.role", user.Role.Value));

            // The caller has a user record only now
            return user.ToUserDto(new Caller.Member(user.Id, user.UserPermissions.Select(p => p.Permission)));
        }
    }
}
