namespace FullstackTemplate.Server.Resources.Email;

using Serilog;

/// <summary>
/// The email sender of the template. It sends nothing. It writes a log with the idempotency key, and with no address
/// and no text, because they are personal data.
/// </summary>
internal sealed class LogEmailSender : IEmailSender
{
    private static readonly ILogger Logger = Log.ForContext<LogEmailSender>();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Logger.Here().Information("Wrote the email {IdempotencyKey} to the log. No email provider sent it",
            message.IdempotencyKey);
        return Task.CompletedTask;
    }
}
