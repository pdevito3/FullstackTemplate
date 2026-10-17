namespace FullstackTemplate.Server.Domain.Users.Features;

using Databases;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QueryKit;
using Resources;

public static class GetUserList
{
    public sealed record Query(UserParametersDto Parameters) : IRequest<PagedList<UserDto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<UserDto>>
    {
        public async Task<PagedList<UserDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            IQueryable<User> query = dbContext.Users
                .TagWithOperationCallSite("users.list")
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyQueryKitFilter(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Id)
                : query.ApplyQueryKitSort(request.Parameters.SortOrder, queryKitConfig);

            var dtos = query.ToUserDtoQueryable();

            return await PagedList<UserDto>.CreateAsync(
                dtos,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
