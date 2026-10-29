namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;
using Resources;
using Serilog;

public static class UpdateWorkshop
{
    public sealed record Command(string Id, WorkshopForUpdateDto Dto) : IRequest<WorkshopDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard)
        : IRequestHandler<Command, WorkshopDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.Update().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.update.load")
                .GetById(request.Id, cancellationToken);
            workshop.GuardChangeBy(caller);

            var workshopForUpdate = request.Dto.ToWorkshopForUpdate();
            var changes = !workshop.HasValuesOf(workshopForUpdate);
            workshop.Update(workshopForUpdate);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (changes)
                _logger.Here().Information("Updated workshop {WorkshopId}", workshop.Id);

            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.update.load_venue")
                .GetById(workshop.VenueId, cancellationToken);
            return workshop.ToWorkshopDto(venue, caller);
        }
    }
}
