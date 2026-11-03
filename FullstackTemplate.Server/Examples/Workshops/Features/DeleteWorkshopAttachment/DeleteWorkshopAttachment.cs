namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Attachments;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Serilog;

public static class DeleteWorkshopAttachment
{
    public sealed record Command(string WorkshopId, string AttachmentId) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard, AttachmentStorage attachments)
        : IRequestHandler<Command>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.WorkshopId);
            Activity.Current?.SetTag("app.attachment_id", request.AttachmentId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.ManageAttachments().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.attachments.delete.load_workshop")
                .AsNoTracking()
                .GetById(request.WorkshopId, cancellationToken);
            workshop.GuardChangeBy(caller);

            // An attachment of another workshop is not found here, so the route cannot delete it
            var attachment = await dbContext.WorkshopAttachments
                .TagWithOperationCallSite("workshops.attachments.delete.load")
                .Where(a => a.WorkshopId == workshop.Id)
                .GetById(request.AttachmentId, cancellationToken);

            await attachments.DeleteAsync(attachment, cancellationToken);

            _logger.Here().Information("Deleted attachment {AttachmentId} of workshop {WorkshopId}", attachment.Id, workshop.Id);
        }
    }
}
