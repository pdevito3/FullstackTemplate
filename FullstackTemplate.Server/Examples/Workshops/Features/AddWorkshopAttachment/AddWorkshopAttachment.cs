namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Users;
using Dtos;
using Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models;
using Resources;
using Resources.Storage;
using Serilog;

public static class AddWorkshopAttachment
{
    /// <param name="WorkshopId">The workshop that gets the file.</param>
    /// <param name="Content">The bytes of the file. The entrypoint owns the stream.</param>
    /// <param name="FileName">The name that the client sent with the file.</param>
    /// <param name="ContentType">The type that the client sent. Null or empty gives application/octet-stream.</param>
    /// <param name="SizeBytes">The length of <paramref name="Content"/>.</param>
    public sealed record Command(
        string WorkshopId,
        Stream Content,
        string FileName,
        string? ContentType,
        long SizeBytes) : IRequest<WorkshopAttachmentDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        IPermissionGuard permissionGuard,
        ObjectStorage storage,
        TimeProvider timeProvider)
        : IRequestHandler<Command, WorkshopAttachmentDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<WorkshopAttachmentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.WorkshopId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.ManageAttachments().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.attachments.add.load_workshop")
                .AsNoTracking()
                .GetById(request.WorkshopId, cancellationToken);
            workshop.GuardChangeBy(caller);

            // The rules run before the upload, so a file that is too large never gets to storage
            var attachment = WorkshopAttachment.Create(workshop, new WorkshopAttachmentForCreation
            {
                FileName = request.FileName,
                ContentType = request.ContentType,
                SizeBytes = request.SizeBytes,
                UploadedAt = timeProvider.GetUtcNow(),
            });
            Activity.Current?.SetTag("app.attachment_id", attachment.Id);

            // The upload is before the lock, because the work of the lock can run more than one time
            await storage.PutAsync(attachment.StorageKey, request.Content, attachment.ContentType, cancellationToken);
            try
            {
                // The shared lock keeps a tenant delete out until the save, so the row does not land in a deleted tenant
                await RootLock.Tenant.RunSharedAsync(dbContext, workshop.TenantId, async ct =>
                {
                    // The soft delete filter applies, so the workshop of a deleted tenant does not exist here
                    await dbContext.Workshops
                        .TagWithOperationCallSite("workshops.attachments.add.check_workshop")
                        .AsNoTracking()
                        .GetById(workshop.Id, ct);

                    await dbContext.WorkshopAttachments.AddAsync(attachment, ct);
                    await dbContext.SaveChangesAsync(ct);
                    return attachment;
                }, cancellationToken);
            }
            catch
            {
                await DeleteObjectWithNoRowAsync(attachment);
                throw;
            }

            _logger.Here().Information("Added attachment {AttachmentId} of {SizeBytes} bytes to workshop {WorkshopId}",
                attachment.Id, attachment.SizeBytes, workshop.Id);
            WorkshopMetrics.RecordAttachmentAdded(attachment.SizeBytes);

            return await attachment.ToWorkshopAttachmentDtoAsync(storage, timeProvider.GetUtcNow());
        }

        // A save can commit and still throw, for example when the reply of the commit is lost and the run again finds
        // the row. So the object goes only when no row names it. An object with no row costs only its storage, but a
        // row with no object is a file that no one can download, so a failed check keeps the object.
        private async Task DeleteObjectWithNoRowAsync(WorkshopAttachment attachment)
        {
            bool saved;
            try
            {
                // Not the request token: a caller that left must not stop the cleanup. No filter applies, so a row
                // that a tenant delete soft-deleted also keeps its object.
                saved = await dbContext.WorkshopAttachments
                    .TagWithOperationCallSite("workshops.attachments.add.check_saved")
                    .IgnoreQueryFilters()
                    .AnyAsync(a => a.Id == attachment.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Telemetry.RecordError(Activity.Current, ex);
                _logger.Here().Error(ex, "Kept the object of attachment {AttachmentId}, because the check for its row failed",
                    attachment.Id);
                return;
            }

            if (saved)
            {
                _logger.Here().Warning("The save of attachment {AttachmentId} failed after its commit, so its object stays",
                    attachment.Id);
                return;
            }

            try
            {
                await storage.DeleteAsync(attachment.StorageKey, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Telemetry.RecordError(Activity.Current, ex);
                _logger.Here().Error(ex, "Could not delete the object of unsaved attachment {AttachmentId}", attachment.Id);
            }
        }
    }
}
