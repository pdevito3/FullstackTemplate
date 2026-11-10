namespace FullstackTemplate.Server.Domain.Attachments;

/// <summary>
/// A file that a record keeps. Each use is its own entity, with its owner, its tenant, and its <see cref="UploadRules"/>,
/// so that each table has its own foreign key. <see cref="AttachmentStorage"/> and the mapper work with each of them.
/// </summary>
public interface IAttachment
{
    string Id { get; }
    string TenantId { get; }
    StoredFile File { get; }
    DateTimeOffset UploadedAt { get; }
}
