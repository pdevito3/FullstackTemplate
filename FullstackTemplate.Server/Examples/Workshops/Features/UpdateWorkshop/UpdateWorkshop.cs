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

            // The form sends the version that it loaded, so a change by another request after that gives 409
            workshop.Update(request.Dto.ToWorkshopForUpdate());
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Updated workshop {WorkshopId}", workshop.Id);

            return workshop.ToWorkshopDto(caller);
        }
    }
}
