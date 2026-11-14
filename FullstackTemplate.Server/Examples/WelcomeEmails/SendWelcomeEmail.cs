namespace FullstackTemplate.Server.Examples.WelcomeEmails;

using System.Diagnostics;
using BackWave.Jobs;
using Databases;
using Microsoft.EntityFrameworkCore;
using Resources;
using Resources.Email;
using Resources.Jobs;
using Serilog;

/// <summary>Sends the welcome email to a new user.</summary>
[Job("send-welcome-email")]
public sealed record SendWelcomeEmail(string TenantId, string UserId) : ITenantJob;

/// <summary>
/// Sends the welcome email to the user of the job. A user that was deleted before the job ran gets no email.
/// </summary>
public sealed class SendWelcomeEmailHandler(JobScope scope, AppDbContext dbContext, IEmailSender email)
    : JobHandler<SendWelcomeEmail>(scope)
{
    private static readonly ILogger Logger = Log.ForContext<SendWelcomeEmailHandler>();

    protected override async Task RunAsync(SendWelcomeEmail job, JobContext context, CancellationToken cancellationToken)
    {
        Activity.Current?.SetTag("app.user_id", job.UserId);
        var user = await dbContext.Users
            .TagWithOperationCallSite("users.welcome_email.load")
            .FirstOrDefaultAsync(u => u.Id == job.UserId, cancellationToken);
        if (user is null)
        {
            Logger.Here().Information("Sent no welcome email to user {UserId}, because the user was deleted", job.UserId);
            return;
        }

        // The key comes from the user, so a run again after a lost lease or a requeue gives the provider the same key
        await email.SendAsync(new EmailMessage(user.Email, "Welcome", $"Hi {user.FirstName}, welcome aboard.",
            IdempotencyKey: $"welcome-email/{user.Id}"), cancellationToken);
        Logger.Here().Information("Sent the welcome email to user {UserId}", user.Id);
    }
}
