namespace FullstackTemplate.Server.Databases;

/// <summary>What a unique index or an exclusion constraint means to the caller when it stops a save.</summary>
/// <param name="Field">The request field of the constraint, which is the key in the errors of the problem.</param>
/// <param name="Message">The message of the duplicate check of the handlers, so that each conflict on the field has one message.</param>
public sealed record ConstraintError(string Field, string Message);
