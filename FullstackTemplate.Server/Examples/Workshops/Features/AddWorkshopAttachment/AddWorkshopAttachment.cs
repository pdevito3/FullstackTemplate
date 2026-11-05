namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Attachments;
using Domain.Attachments.Dtos;
using Domain.Attachments.Mappings;
using Domain.Users;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Models;
using Resources;
using Resources.Storage;
using Serilog;

public static class AddWorkshopAttachment
{
    public sealed record Command(string WorkshopId, ReceivedFile File) : IRequest<AttachmentDto>;

    public sealed class Handler(
        AppDbContext dbContext,
        IPermissionGuard permissionGuard,
        AttachmentStorage attachments,
        ObjectStorage storage,
        TimeProvider timeProvider)
        : IRequestHandler<Command, AttachmentDto>
    {
        private readonly ILogger _logger = Log.ForContext<Handler>();

        public async Task<AttachmentDto> Handle(Command request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.WorkshopId);

            var caller = await permissionGuard.GetCallerAsync(cancellationToken);
            WorkshopAction.ManageAttachments().GuardCaller(caller);

            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.attachments.add.load_workshop")
                .AsNoTracking()
                .GetById(request.WorkshopId, cancellationToken);
            workshop.GuardChangeBy(caller);

            // The rules run before the upload, so a file that they refuse never gets to storage
            var upload = await FileUpload.ReadAsync(request.File, cancellationToken);
            var attachment = WorkshopAttachment.Create(workshop, new WorkshopAttachmentForCreation
            {
                File = upload,
                UploadedAt = timeProvider.GetUtcNow(),
            });
            Activity.Current?.SetTag("app.attachment_id", attachment.Id);

            // The upload is before the lock, because the work of the lock can run more than one time
            await attachments.AddAsync(attachment, request.File, async () =>
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
                }, cancellationToken), cancellationToken);

            _logger.Here().Information("Added attachment {AttachmentId} of {SizeBytes} bytes to workshop {WorkshopId}",
                attachment.Id, attachment.File.SizeBytes, workshop.Id);

            return await attachment.ToAttachmentDtoAsync(storage, timeProvider.GetUtcNow());
        }
    }
}
