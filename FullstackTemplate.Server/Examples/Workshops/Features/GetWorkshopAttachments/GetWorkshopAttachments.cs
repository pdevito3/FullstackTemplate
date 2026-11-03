namespace FullstackTemplate.Server.Examples.Workshops.Features;

using System.Diagnostics;
using Databases;
using Domain.Attachments.Dtos;
using Domain.Attachments.Mappings;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Resources.Storage;

public static class GetWorkshopAttachments
{
    public sealed record Query(string WorkshopId) : IRequest<AttachmentListDto>;

    public sealed class Handler(AppDbContext dbContext, ObjectStorage storage, TimeProvider timeProvider)
        : IRequestHandler<Query, AttachmentListDto>
    {
        public async Task<AttachmentListDto> Handle(Query request, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("app.workshop_id", request.WorkshopId);

            // The tenant filter gives 404 for a workshop of another tenant, so no URL of its files is signed
            var workshop = await dbContext.Workshops
                .TagWithOperationCallSite("workshops.attachments.list.load_workshop")
                .AsNoTracking()
                .GetById(request.WorkshopId, cancellationToken);

            var attachments = await dbContext.WorkshopAttachments
                .TagWithOperationCallSite("workshops.attachments.list")
                .AsNoTracking()
                .Where(a => a.WorkshopId == workshop.Id)
                .OrderBy(a => a.UploadedAt)
                .ThenBy(a => a.Id)
                .ToListAsync(cancellationToken);
            Activity.Current?.SetTag("app.attachment_count", attachments.Count);

            return await attachments.ToAttachmentListDtoAsync(WorkshopAttachment.UploadRules, storage,
                timeProvider.GetUtcNow());
        }
    }
}
