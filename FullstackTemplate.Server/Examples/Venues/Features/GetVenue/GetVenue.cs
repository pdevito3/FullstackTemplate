namespace FullstackTemplate.Server.Examples.Venues.Features;

using System.Diagnostics;
using Databases;
using Dtos;
using Mappings;
using MediatR;

public static class GetVenue
{
    public sealed record Query(string Id) : IRequest<VenueDto>;

    public sealed class Handler(AppDbContext dbContext) : IRequestHandler<Query, VenueDto>
    {
        public async Task<VenueDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.venue_id", request.Id);

            var venue = await dbContext.Venues
                .TagWithOperationCallSite("venues.get_by_id")
                .GetById(request.Id, cancellationToken);
            return venue.ToVenueDto();
        }
    }
}
