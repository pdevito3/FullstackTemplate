namespace FullstackTemplate.Server.Domain.Users;

using Databases;
using Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Services;

/// <summary>
/// Stops a request that has <see cref="IRequireTenantAdmin"/> with a 403 when the caller is not an admin.
/// The tenant filter still applies in the handler, so an admin can change only their own tenant.
/// A caller that can manage all tenants (a trusted tenant management client or a system scope) also passes.
/// For a request with <see cref="IAllowSelf"/>, the user that the request is about also passes.
/// </summary>
public sealed class TenantAdminBehavior<TRequest, TResponse>(
    AppDbContext dbContext,
    ICurrentUserService currentUserService,
    ITenantScopeProvider tenantScopeProvider) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequireTenantAdmin
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!tenantScopeProvider.CanManageAllTenants() && !await CallerCanSendAsync(request, cancellationToken))
            throw new ForbiddenAccessException("Only an admin can do this.");

        return await next(cancellationToken);
    }

    private async Task<bool> CallerCanSendAsync(TRequest request, CancellationToken cancellationToken)
    {
        var identifier = currentUserService.UserIdentifier;
        if (identifier is null)
            return false;

        var caller = await dbContext.Users
            .TagWithOperationCallSite("users.current_user_role")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Identifier == identifier, cancellationToken);
        if (caller is null)
            return false;

        return caller.Role.IsAdmin() || (request is IAllowSelf self && self.SelfUserId == caller.Id);
    }
}
