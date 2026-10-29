namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Exceptions;
using Mappings;
using MediatR;
using Resources;
using Serilog;

public static class ScheduleWorkshop
{
    public sealed record Command(string Id, WorkshopForSchedulingDto Dto) : IRequest<WorkshopDto>;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard, TimeProvider timeProvider)
        : IRequestHandler<Command, WorkshopDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.Id);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.Schedule().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.schedule.load")
                .GetById(request.Id, cancellationToken);
            Activity.Current?.SetTag("app.venue_id", workshop.VenueId);
            workshop.GuardChangeBy(caller);

            var schedule = await LoadCaseAsync(workshop, request.Dto.Version, cancellationToken);
            var wasScheduled = workshop.Status == WorkshopStatus.Scheduled();

            switch (WorkshopScheduling.Decide(schedule))
            {
                case ScheduleDecision.Offered offered:
                    workshop.Schedule(offered.Start);
                    break;
                case ScheduleDecision.Refused { Refusal: ScheduleRefusal.StaleVersion }:
                    Activity.Current?.SetTag("app.workshop.schedule_refusal", "stale_version");
                    throw new StaleVersionException();
                case ScheduleDecision.Refused { Refusal: ScheduleRefusal.WrongStatus wrongStatus }:
                    throw Refuse("wrong_status", wrongStatus);
                case ScheduleDecision.Refused { Refusal: ScheduleRefusal.NotOffered notOffered }:
                    throw Refuse("not_offered", notOffered);
                default:
                    throw new UnreachableException();
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (!wasScheduled)
            {
                _logger.Here().Information("Scheduled workshop {WorkshopId} at venue {VenueId}", workshop.Id, workshop.VenueId);
                WorkshopMetrics.RecordStatusChange(workshop.Status);
            }

            return workshop.ToWorkshopDto(schedule.Venue, caller);
        }

        private async Task<ScheduleCase> LoadCaseAsync(Workshop workshop, uint version, CancellationToken cancellationToken)
        {
            var venue = await dbContext.Venues
                .TagWithOperationCallSite("workshops.schedule.load_venue")
                .GetById(workshop.VenueId, cancellationToken);

            return new ScheduleCase(workshop, venue, timeProvider.GetUtcNow(), version);
        }

        private static ValidationException Refuse(string kind, ScheduleRefusal refusal)
        {
            Activity.Current?.SetTag("app.workshop.schedule_refusal", kind);
            return new ValidationException(refusal.Field, refusal.Message);
        }
    }
}
