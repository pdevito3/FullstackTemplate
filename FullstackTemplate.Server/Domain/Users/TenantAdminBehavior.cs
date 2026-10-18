namespace FullstackTemplate.Server.Domain.Users;

using Databases;
using Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Services;

/// <summary>
/// Stops a request that has <see cref="IRequireTenantAdmin"/> with a 403 when the caller is not an admin.
/// The tenant filter still applies in the handler, so an admin can change only their own tenant.
/// A caller that can manage all tenants (an allow-listed machine client or a system scope) also passes.
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
        if (!tenantScopeProvider.CanManageAllTenants() && !await CallerIsAdminAsync(cancellationToken))
            throw new ForbiddenAccessException("Only an admin can do this.");

        return await next(cancellationToken);
    }

    private async Task<bool> CallerIsAdminAsync(CancellationToken cancellationToken)
    {
        var identifier = currentUserService.UserIdentifier;
        if (identifier is null)
            return false;

        var caller = await dbContext.Users
            .TagWithOperationCallSite("users.current_user_role")
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Identifier == identifier, cancellationToken);

        return caller is not null && caller.Role.IsAdmin();
    }
}
