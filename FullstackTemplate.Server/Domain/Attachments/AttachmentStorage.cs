namespace FullstackTemplate.Server.Domain.Attachments;

using Databases;
using Microsoft.Extensions.Options;
using ObjectDeletes;
using Resources.Storage;

/// <summary>
/// Keeps the row of each attachment and its object in storage in step. A row with no object is a file that no one can
/// download, so the object goes first on an add and last on a delete. An object with no row costs only its storage, and
/// the sweep of the bucket deletes it.
/// </summary>
public sealed class AttachmentStorage(AppDbContext dbContext, ObjectStorage storage, IOptions<AttachmentOptions> options)
{
    /// <summary>
    /// Puts the bytes of a new attachment in storage, then runs the save of its row. When the put or the save throws,
    /// the object stays, and <see cref="SweepObjects"/> deletes it when no row names it. A save can throw while its
    /// COMMIT still lands, so only the sweep, after the grace, can know that no row names the object.
    /// </summary>
    /// <param name="attachment">The new attachment, which passed the upload rules of its use.</param>
    /// <param name="file">The file that the attachment came from. The put sends all of its bytes.</param>
    /// <param name="saveRow">Adds the row and saves it, and stops on the token that it gets. The put is already done, so
    /// the save can run in a lock that runs its work again.</param>
    /// <param name="cancellationToken">Stops the put and the save.</param>
    /// <exception cref="TimeoutException">The put and the save took longer than <see cref="AttachmentOptions.AddTimeout"/>.</exception>
    public async Task AddAsync<TAttachment>(TAttachment attachment, ReceivedFile file, Func<CancellationToken, Task> saveRow,
        CancellationToken cancellationToken)
        where TAttachment : BaseEntity, IAttachment
    {
        file.Content.Position = 0;
        // The sweep of the bucket keeps an object only while it is younger than the time limit and a margin, so an add
        // must end within the limit, also when its caller does not cancel it.
        var addTimeout = options.Value.AddTimeout;
        using var add = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        add.CancelAfter(addTimeout);
        try
        {
            await storage.PutAsync(attachment.File.StorageKey, file.Content, attachment.File.MediaType, add.Token);
            await saveRow(add.Token);
        }
        catch (OperationCanceledException exception)
            when (add.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The add of attachment {attachment.Id} took more than {addTimeout}.", exception);
        }

        AttachmentMetrics.RecordAdded<TAttachment>(attachment.File.SizeBytes);
    }

    /// <summary>
    /// Deletes the row of the attachment. The save also queues the <see cref="DeleteObject"/> job of its
    /// object, and BackWave runs the job after the commit.
    /// </summary>
    public async Task DeleteAsync<TAttachment>(TAttachment attachment, CancellationToken cancellationToken)
        where TAttachment : BaseEntity, IAttachment
    {
        dbContext.Remove(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);
        AttachmentMetrics.RecordDeleted<TAttachment>();
    }
}
