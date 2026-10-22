namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// The record changed after the caller loaded it. Maps to 409 with the message under <see cref="Field"/>.
/// </summary>
public sealed class ConcurrencyConflictException(Exception? innerException = null)
    : ConflictException(Field, StaleMessage, innerException)
{
    public const string Field = "version";
    public const string StaleMessage = "This record changed after you loaded it. Reload it, then make your change again.";
}
