namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;

public static class GetWorkshop
{
    public sealed record Query(string Id) : IRequest<WorkshopDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Query, WorkshopDto>
    {
        public async Task<WorkshopDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.Id);

            // Each member of the tenant reads the workshops. The caller decides only the allowed actions.
            var caller = await permissionGuard.GetCallerAsync(cancellationToken);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.get_by_id")
                .GetById(request.Id, cancellationToken);
            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.get_by_id.load_venue")
                .GetById(workshop.VenueId, cancellationToken);
            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
