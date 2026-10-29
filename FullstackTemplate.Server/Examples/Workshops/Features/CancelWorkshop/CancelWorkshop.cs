namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;

public static class CancelWorkshop
{
    public sealed record Command(string Id) : IRequest<WorkshopDto>, IRunsAgainWhenRaced;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, WorkshopDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.Cancel().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.cancel.load")
                .GetById(request.Id, cancellationToken);
            workshop.GuardChangeBy(caller);
            Activity.Current?.SetTag("app.venue_id", workshop.VenueId);

            var wasCancelled = workshop.Status == WorkshopStatus.Cancelled();
            workshop.Cancel();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!wasCancelled)
            {
                _logger.Here().Information("Cancelled workshop {WorkshopId}", workshop.Id);
                WorkshopMetrics.RecordStatusChange(workshop.Status);
            }

            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.cancel.load_venue")
                .GetById(workshop.VenueId, cancellationToken);
            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
