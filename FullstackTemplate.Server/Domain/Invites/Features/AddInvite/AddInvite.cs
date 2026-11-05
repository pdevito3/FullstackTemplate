namespace FullstackTemplate.Server.Domain.Invites.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Tenants;
using Users;

public static class AddInvite
{
    public sealed record Command(InviteForCreationDto Dto) : IRequest<AddInviteResponseDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard, TimeProvider timeProvider)
        : IRequestHandler<Command, AddInviteResponseDto>
    {
        // The key in the errors of the problem is the name of the request field
        private const string TenantIdField = "tenantId";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<AddInviteResponseDto> Handle(Command request, CancellationToken cancellationToken)
        {
            // The requested tenant first, so that a refused request also has it on the span
            Activity.Current?.SetTag("app.tenant_id", request.Dto.TenantId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            TenantAction.InviteUser().GuardCaller(caller);
            var role = UserRole.Of(request.Dto.Role);
            Activity.Current?.SetTag("app.invite.role", role.Value);
            // A caller cannot invite with a role that has more permissions than the caller has (INV-1)
            caller.Require(role.GetDefaultPermissions());

            var tenantId = ResolveTenantId(request.Dto.TenantId);
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            var token = InviteToken.New();
            // The shared lock keeps a tenant delete out until the save, so the invite does not land in a deleted tenant.
            var invite = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
            {
                var tenantExists = await dbContext.Tenants
                    .TagWithOperationCallSite("invites.add.tenant_exists")
                    .AnyAsync(t => t.Id == tenantId, ct);
                if (!tenantExists)
                    throw new ValidationException(TenantIdField, "The tenant does not exist.");

                var added = Invite.Create(request.Dto.ToInviteForCreation(tenantId), token, timeProvider.GetUtcNow());
                Activity.Current?.SetTag("app.invite_id", added.Id);

                await dbContext.Invites.AddAsync(added, ct);
                await dbContext.SaveChangesAsync(ct);
                return added;
            }, cancellationToken);

            _logger.Here().Information("Added invite {InviteId} to tenant {TenantId} with role {Role}",
                invite.Id, tenantId, role.Value);

            return new AddInviteResponseDto { Invite = invite.ToInviteDto(caller, timeProvider.GetUtcNow()), Token = token.Value };
        }

        // A user invites to their own tenant. A caller that sees all tenants has no tenant of its own,
        // so it must name the tenant.
        private string ResolveTenantId(string? requestedTenantId)
        {
            var scope = dbContext.CurrentTenantScope;
            if (string.IsNullOrWhiteSpace(requestedTenantId))
            {
                if (scope.TenantId is { } ownTenantId)
                    return ownTenantId;
                if (scope.IncludesAllTenants)
                    throw new ValidationException(TenantIdField, "Please provide a tenant.");
                throw new ForbiddenAccessException("You can invite a colleague only to your own tenant.");
            }

            if (!scope.Includes(requestedTenantId))
                throw new ForbiddenAccessException("You can invite a colleague only to your own tenant.");

            if (!EntityId.IsValid(requestedTenantId, Tenant.IdPrefix))
                throw new ValidationException(TenantIdField, "The tenant does not exist.");

            return requestedTenantId;
        }
    }
}
