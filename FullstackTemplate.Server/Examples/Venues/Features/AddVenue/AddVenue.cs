namespace FullstackTemplate.Server.Examples.Venues.Features;

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

public static class AddVenue
{
    public sealed record Command(VenueForCreationDto Dto) : IRequest<VenueDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, VenueDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<VenueDto> Handle(Command request, CancellationToken cancellationToken)
        {
            await permissionGuard.RequireAsync(Permission.ManageWorkshops(), cancellationToken);

            var tenantId = dbContext.CurrentTenantScope.TenantId
                ?? throw new ForbiddenAccessException("You can add a venue only to your own tenant.");
            Activity.Current?.SetTag("app.tenant_id", tenantId);

            // The shared lock keeps a tenant delete out until the save, so the venue does not land in a deleted tenant.
            var added = await RootLock.Tenant.RunSharedAsync(dbContext, tenantId, async ct =>
            {
                var tenantExists = await dbContext.Tenants
                    .TagWithOperationCallSite("venues.add.tenant_exists")
                    .AnyAsync(t => t.Id == tenantId, ct);
                if (!tenantExists)
                    throw new NotFoundException("The tenant does not exist.");

                var venue = Venue.Create(request.Dto.ToVenueForCreation(tenantId));
                Activity.Current?.SetTag("app.venue_id", venue.Id);

                await dbContext.Venues.AddAsync(venue, ct);
                await dbContext.SaveChangesAsync(ct);
                return venue;
            }, cancellationToken);

            _logger.Here().Information("Added venue {VenueId} in time zone {TimeZone}", added.Id, added.TimeZone.Value);

            return added.ToVenueDto();
        }
    }
}
