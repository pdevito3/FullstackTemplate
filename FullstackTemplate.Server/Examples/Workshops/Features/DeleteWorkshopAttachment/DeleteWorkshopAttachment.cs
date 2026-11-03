namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources;
using Resources.Storage;
using Serilog;

public static class DeleteWorkshopAttachment
{
    public sealed record Command(string WorkshopId, string AttachmentId) : IRequest;

    public sealed class Handler(AppDbContext dbContext, IPermissionGuard permissionGuard, ObjectStorage storage)
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

            dbContext.WorkshopAttachments.Remove(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.Here().Information("Deleted attachment {AttachmentId} of workshop {WorkshopId}", attachment.Id, workshop.Id);
            WorkshopMetrics.RecordAttachmentDeleted();

            // After the commit, so a failed save keeps the file. A failure here leaves an object that no row names.
            try
            {
                // Not the request token: a caller that left must not stop the delete of the bytes
                await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Telemetry.RecordError(Activity.Current, ex);
                _logger.Here().Error(ex, "Could not delete the object of deleted attachment {AttachmentId}", attachment.Id);
            }
        }
    }
}
