namespace FullstackTemplate.Server.Examples.Workshops.Features;

using Databases;
using Domain.Users;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class GetWorkshopList
{
    public sealed record Query(WorkshopParametersDto Parameters) : IRequest<PagedList<WorkshopDto>>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, PagedList<WorkshopDto>>
    {
        private const string SearchParameter = "search";

        public async Task<PagedList<WorkshopDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            // Each member of the tenant reads the workshops. The caller decides only the allowed actions.
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);

            var queryKitConfig = new CustomQueryKitConfiguration();

            var workshops = dbContext.Workshops
                .TagWithOperationCallSite("workshops.list")
                .AsNoTracking();

            if (request.Parameters.Search is { } search)
            {
                if (search.Length > WorkshopParametersDto.SearchMaxLength)
                    throw new QueryInputException(SearchParameter,
                        $"A search can have no more than {WorkshopParametersDto.SearchMaxLength} characters.");
                if (!PostgresText.CanStore(search))
                    throw new QueryInputException(SearchParameter, "The search has a character that is not allowed.");

                var pattern = LikePattern.Containing(search);
                workshops = workshops.Where(w => EF.Functions.ILike(w.Title, pattern, LikePattern.EscapeCharacter));
            }

            // The filter and the sort run on the DTO, so a caller can query only the fields that the contract shows
            var query = workshops.ToWorkshopDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            // Paging needs a fixed order, or a row can show on two pages or on none
            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(w => w.StartsAt).ThenBy(w => w.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(w => w.Id);

            var page = await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
            page.AddAllowedActions(caller);
            return page;
        }
    }
}
