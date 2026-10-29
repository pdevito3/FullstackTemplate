namespace FullstackTemplate.Server.Examples.Venues.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
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

            var venue = Venue.Create(request.Dto.ToVenueForCreation(tenantId));
            Activity.Current?.SetTag("app.venue_id", venue.Id);

            await dbContext.Venues.AddAsync(venue, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Added venue {VenueId} in time zone {TimeZone}", venue.Id, venue.TimeZone.Value);

            return venue.ToVenueDto();
        }
    }
}
