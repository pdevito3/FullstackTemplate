namespace FullstackTemplate.Server.Examples.Workshops;

using Domain;
using Domain.Attachments;
using DomainEvents;
using Models;

public class WorkshopAttachment : BaseEntity, ITenantable, IAttachment
{
    public const string IdPrefix = "wkatt";

    /// <summary>The files that a workshop takes: images that show in the page, and PDF documents.</summary>
    public static readonly UploadRules UploadRules =
        new(10, FileType.Png, FileType.Jpeg, FileType.Gif, FileType.WebP, FileType.Pdf);

    [NotSensitive] public string TenantId { get; private set; } = default!;
    [NotSensitive] public string WorkshopId { get; private set; } = default!;
    // A file name can name a person, such as "Jane Doe passport.pdf"
    [Sensitive] public StoredFile File { get; private set; } = default!;
    [NotSensitive] public DateTimeOffset UploadedAt { get; private set; }

    public static WorkshopAttachment Create(Workshop workshop, WorkshopAttachmentForCreation attachmentForCreation)
    {
        var attachment = new WorkshopAttachment
        {
            TenantId = workshop.TenantId,
            WorkshopId = workshop.Id,
            UploadedAt = attachmentForCreation.UploadedAt.ToUniversalTime(),
        };
        if (attachmentForCreation.Id is not null)
            attachment.SetId(attachmentForCreation.Id);

        attachment.File = StoredFile.Accept(attachmentForCreation.File, UploadRules, attachment.TenantId,
            $"workshops/{attachment.WorkshopId}/attachments/{attachment.Id}");
        attachment.QueueDomainEvent(new WorkshopAttachmentAdded(attachment.Id, attachment.WorkshopId));

        return attachment;
    }

    protected WorkshopAttachment() : base(IdPrefix) { } // For EF Core
}
