namespace FullstackTemplate.IntegrationTests.Resources.Email;

using System.Collections.Concurrent;
using FullstackTemplate.Server.Resources.Email;

/// <summary>An email sender that keeps each message that it gets, in the order of the calls. It sends nothing.</summary>
/// <param name="lostReplies">
/// The number of first calls that keep the message and then throw, as a call does when the provider takes the email
/// and the reply does not come back.
/// </param>
public sealed class RecordingEmailSender(int lostReplies = 0) : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private int _calls;

    public IReadOnlyList<EmailMessage> Sent => [.. _sent];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        if (Interlocked.Increment(ref _calls) <= lostReplies)
            throw new TimeoutException("The email provider took the email, and its reply did not come back.");
        return Task.CompletedTask;
    }
}
