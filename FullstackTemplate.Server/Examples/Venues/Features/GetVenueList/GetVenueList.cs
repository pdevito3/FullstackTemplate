namespace FullstackTemplate.Server.Examples.Venues.Features;

using Databases;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;

public static class GetVenueList
{
    public sealed record Query(VenueParametersDto Parameters) : IRequest<PagedList<VenueDto>>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, PagedList<VenueDto>>
    {
        private const string SearchParameter = "search";

        public async Task<PagedList<VenueDto>> Handle(Query request, CancellationToken cancellationToken)
        {
            var queryKitConfig = new CustomQueryKitConfiguration();

            var venues = dbContext.Venues
                .TagWithOperationCallSite("venues.list")
                .AsNoTracking();

            if (request.Parameters.Search is { } search)
            {
                if (search.Length > VenueParametersDto.SearchMaxLength)
                    throw new QueryInputException(SearchParameter,
                        $"A search can have no more than {VenueParametersDto.SearchMaxLength} characters.");
                if (!PostgresText.CanStore(search))
                    throw new QueryInputException(SearchParameter, "The search has a character that is not allowed.");

                var pattern = LikePattern.Containing(search);
                venues = venues.Where(v => EF.Functions.ILike(v.Name, pattern, LikePattern.EscapeCharacter));
            }

            var query = venues.ToVenueDtoQueryable();

            if (!string.IsNullOrWhiteSpace(request.Parameters.Filters))
                query = query.ApplyFilters(request.Parameters.Filters, queryKitConfig);

            query = string.IsNullOrWhiteSpace(request.Parameters.SortOrder)
                ? query.OrderBy(v => v.Name).ThenBy(v => v.Id)
                : query.ApplySortOrder(request.Parameters.SortOrder, queryKitConfig).ThenBy(v => v.Id);

            return await PagedList.CreateAsync(
                query,
                request.Parameters.PageNumber,
                request.Parameters.PageSize,
                cancellationToken);
        }
    }
}
