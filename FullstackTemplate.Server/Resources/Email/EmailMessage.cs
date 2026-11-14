namespace FullstackTemplate.Server.Resources.Email;

using Domain.EmailAddresses;

/// <summary>
/// An email to one address. Make the idempotency key from the effect, such as the ID of the user, so that a job that
/// runs again gives the same key.
/// </summary>
public sealed record EmailMessage(EmailAddress To, string Subject, string TextBody, string IdempotencyKey);
