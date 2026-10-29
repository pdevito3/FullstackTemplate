namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;

public static class CompleteWorkshop
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
            WorkshopAction.Complete().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.complete.load")
                .GetById(request.Id, cancellationToken);
            workshop.GuardChangeBy(caller);
            Activity.Current?.SetTag("app.venue_id", workshop.VenueId);

            var wasCompleted = workshop.Status == WorkshopStatus.Completed();
            workshop.Complete();
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!wasCompleted)
            {
                _logger.Here().Information("Completed workshop {WorkshopId}", workshop.Id);
                WorkshopMetrics.RecordStatusChange(workshop.Status);
            }

            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.complete.load_venue")
                .GetById(workshop.VenueId, cancellationToken);
            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
