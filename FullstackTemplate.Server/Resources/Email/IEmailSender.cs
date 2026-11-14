namespace FullstackTemplate.Server.Resources.Email;

/// <summary>
/// Sends an email. The Server registers <see cref="LogEmailSender"/>. Replace it with a sender for your email provider.
/// Send an email only from a job, after the commit of the save that asks for it.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends <paramref name="message"/>. A sender for a provider that takes an idempotency key gives it the key of the
    /// message, so that the provider sends each key one time.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
