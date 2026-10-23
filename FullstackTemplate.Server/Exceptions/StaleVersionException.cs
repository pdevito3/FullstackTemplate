namespace FullstackTemplate.Server.Exceptions;

/// <summary>
/// The caller based the change on a version of the record that is no longer current.
/// </summary>
public sealed class StaleVersionException(Exception? innerException = null)
    : Exception(StaleMessage, innerException)
{
    public const string Field = "version";
    public const string StaleMessage = "This record changed after you loaded it. Reload it, then make your change again.";
}
