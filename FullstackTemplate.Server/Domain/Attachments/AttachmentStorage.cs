namespace FullstackTemplate.Server.Domain.Attachments;

using System.Diagnostics;
using Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ObjectDeletes;
using Resources;
using Resources.Storage;
using Serilog;

/// <summary>
/// Keeps the row of each attachment and its object in storage in step. A row with no object is a file that no one can
/// download, so the object goes first on an add and last on a delete. An object with no row costs only its storage.
/// </summary>
public sealed class AttachmentStorage(AppDbContext dbContext, ObjectStorage storage, IOptions<AttachmentOptions> options,
    IOptions<ObjectDeleteOptions> deleteOptions)
{
    private readonly ILogger _logger = Log.ForContext<AttachmentStorage>();

    /// <summary>
    /// Puts the bytes of a new attachment in storage, then runs the save of its row. When the put or the save throws,
    /// the object goes only when no row names it, and the exception goes on to the caller. A put that throws can still
    /// have stored the bytes, for example when its reply is lost.
    /// </summary>
    /// <param name="attachment">The new attachment, which passed the upload rules of its use.</param>
    /// <param name="file">The file that the attachment came from. The put sends all of its bytes.</param>
    /// <param name="saveRow">Adds the row and saves it, and stops on the token that it gets. The put is already done, so
    /// the save can run in a lock that runs its work again.</param>
    /// <param name="cancellationToken">Stops the put and the save. The cleanup after a failed put or save does not stop on
    /// it, and its delete stops after <see cref="ObjectDeleteOptions.AttemptTimeout"/>.</param>
    /// <exception cref="TimeoutException">The put and the save took longer than <see cref="AttachmentOptions.AddTimeout"/>.</exception>
    public async Task AddAsync<TAttachment>(TAttachment attachment, ReceivedFile file, Func<CancellationToken, Task> saveRow,
        CancellationToken cancellationToken)
        where TAttachment : BaseEntity, IAttachment
    {
        file.Content.Position = 0;
        try
        {
            await PutAndSaveInAddTimeAsync(attachment, file, saveRow, cancellationToken);
        }
        catch
        {
            await DeleteObjectWithNoRowAsync(attachment);
            throw;
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

    // The sweep of the bucket keeps an object only while it is younger than the time limit and a margin, so an add must
    // end within the limit, also when its caller does not cancel it.
    private async Task PutAndSaveInAddTimeAsync<TAttachment>(TAttachment attachment, ReceivedFile file,
        Func<CancellationToken, Task> saveRow, CancellationToken cancellationToken)
        where TAttachment : BaseEntity, IAttachment
    {
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
    }

    // A save can commit and still throw, for example when the reply of the commit is lost and the run again finds the
    // row. So the object goes only when no row names it, and a failed check keeps the object.
    private async Task DeleteObjectWithNoRowAsync<TAttachment>(TAttachment attachment)
        where TAttachment : BaseEntity, IAttachment
    {
        bool saved;
        try
        {
            // Not the request token: a caller that left must not stop the cleanup. No filter applies, so a row that a
            // tenant delete soft-deleted also keeps its object.
            saved = await dbContext.Set<TAttachment>()
                .TagWithOperationCallSite("attachments.add.check_saved")
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
            await ObjectDeletes.InAttemptTimeAsync(deleteOptions.Value.AttemptTimeout, async attempt =>
            {
                await storage.DeleteAsync(attachment.File.StorageKey, attempt);
                return 1;
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(Activity.Current, ex);
            _logger.Here().Error(ex, "Could not delete the object of unsaved attachment {AttachmentId}", attachment.Id);
        }
    }
}
