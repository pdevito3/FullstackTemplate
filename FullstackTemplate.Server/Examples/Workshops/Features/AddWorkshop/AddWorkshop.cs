namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class AddWorkshop
{
    public sealed record Command(WorkshopForCreationDto Dto) : IRequest<WorkshopDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, WorkshopDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.venue_id", request.Dto.VenueId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageWorkshops());

            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.add.load_venue")
                .FirstOrDefaultAsync(v => v.Id == request.Dto.VenueId, cancellationToken)
                ?? throw new ValidationException("venueId", "The venue does not exist.");

            var organizerId = (caller as Caller.Member)?.UserId;
            var workshop = Workshop.Create(venue, request.Dto.ToWorkshopForCreation(organizerId));
            Activity.Current?.SetTag("app.workshop_id", workshop.Id);

            await dbContext.Workshops.AddAsync(workshop, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Added workshop {WorkshopId} at venue {VenueId}", workshop.Id, venue.Id);

            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
