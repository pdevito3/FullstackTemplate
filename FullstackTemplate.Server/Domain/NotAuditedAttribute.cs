namespace FullstackTemplate.Server.Domain;

/// <summary>
/// The audit log does not record the rows of this entity type, so the write guard lets ExecuteUpdate, ExecuteDelete,
/// and raw SQL write its table. The save audits each <see cref="BaseEntity"/>, so only a type that is not a
/// <see cref="BaseEntity"/> can have this mark, and each such type of the model must have it. Unit tests enforce both.
/// </summary>
/// <param name="reason">Why the rows need no audit, for the reader of the type.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NotAuditedAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
