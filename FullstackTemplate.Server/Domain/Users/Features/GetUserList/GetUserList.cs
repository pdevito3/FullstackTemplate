namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class GetUserList
{
    public sealed record Query(UserParametersDto Parameters) : IRequest<PagedList<UserDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<UserDto>>
    {
        public async Task<PagedList<UserDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ReadUsers());

            var queryKitConfig = new CustomQueryKitConfiguration();

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = dbContext.Users
                .TagWithOperationCallSite("users.list")
                .AsNoTracking()
                .ToUserDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(u => u.Id);

            var users = await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
            users.AddAllowedActions(caller);
            return users;
        }
    }
}
