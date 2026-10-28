namespace FullstackTemplate.Server.Databases;

/// <summary>What a unique index or an exclusion constraint means to the caller when it stops a save.</summary>
public abstract record ConstraintError
{
    private ConstraintError()
    {
    }

    /// <summary>The value conflicts with a record that exists. The caller must change the field.</summary>
    /// <param name="Field">The request field of the constraint, which is the key in the errors of the problem.</param>
    /// <param name="Message">The message of the duplicate check of the handlers, so that each conflict on the field has one message.</param>
    public sealed record RequestField(string Field, string Message) : ConstraintError;

    /// <summary>
    /// Another user changed the record after the caller loaded it, for example a second current revision of one
    /// document.
    /// </summary>
    public sealed record StaleVersion : ConstraintError;
}
