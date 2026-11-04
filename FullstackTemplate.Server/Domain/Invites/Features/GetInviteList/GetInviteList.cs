namespace FullstackTemplate.Server.Domain.Invites.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Users;

public static class GetInviteList
{
    public sealed record Query(InviteParametersDto Parameters) : IRequest<PagedList<InviteDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<InviteDto>>
    {
        public async Task<PagedList<InviteDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageUsers());

            var queryKitConfig = new CustomQueryKitConfiguration();

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = dbContext.Invites
                .TagWithOperationCallSite("invites.list")
                .AsNoTracking()
                .ToInviteDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // The newest invite first. Each invite expires a fixed time after its creation.
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderByDescending(x => x.ExpiresAt).ThenBy(x => x.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(x => x.Id);

            var invites = await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
            invites.AddAllowedActions(caller);
            return invites;
        }
    }
}
