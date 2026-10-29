namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain;
using Domain.Users;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;
using Venues;

public static class AddWorkshop
{
    public sealed record Command(WorkshopForCreationDto Dto) : IRequest<WorkshopDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, WorkshopDto>
    {
        private const string VenueIdField = "venueId";

        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.venue_id", request.Dto.VenueId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            caller.Require(Permission.ManageWorkshops());

            if (!EntityId.IsValid(request.Dto.VenueId, Venue.IdPrefix))
                throw new ValidationException(VenueIdField, "The venue does not exist.");

            // A caller that sees all tenants can name a venue of any tenant, so the tenant of the lock comes from the venue.
            var tenantId = await dbContext.Venues
                .TagWithOperationCallSite("workshops.add.venue_tenant")
                .Where(v => v.Id == request.Dto.VenueId)
                .Select(v => v.TenantId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new ValidationException(VenueIdField, "The venue does not exist.");
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            // The shared lock keeps a tenant delete out until the save, so the workshop does not land in a deleted tenant.
            var (workshop, venue) = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
            {
                // The soft delete filter applies, so the venue of a deleted tenant does not exist here.
                var venue = await dbContext.Venues
                    .TagWithOperationCallSite("workshops.add.load_venue")
                    .FirstOrDefaultAsync(v => v.Id == request.Dto.VenueId, ct)
                    ?? throw new ValidationException(VenueIdField, "The venue does not exist.");

                var organizerId = (caller as Caller.Member)?.UserId;
                var workshop = Workshop.Create(venue, request.Dto.ToWorkshopForCreation(organizerId));
                Activity.Current?.SetTag("app.workshop_id", workshop.Id);

                await dbContext.Workshops.AddAsync(workshop, ct);
                await dbContext.SaveChangesAsync(ct);
                return (workshop, venue);
            }, cancellationToken);

            _logger.Here().Information("Added workshop {WorkshopId} at venue {VenueId}", workshop.Id, venue.Id);

            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
