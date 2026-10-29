namespace FullstackTemplate.Server.Domain;

/// <summary>
/// An entity whose delete queues a domain event, so that a handler can change the links of another domain to it
/// (rule 11 of <c>docs/backend/domain-events.md</c>). Remove it with <c>Remove(entity.Delete())</c>. The save refuses
/// a plain <c>Remove</c>, and a tenant delete calls <see cref="Delete"/> for each one.
/// </summary>
public interface IDeletedWithEvent
{
    /// <summary>Queues the delete event and returns the entity, for <c>Remove</c>.</summary>
    BaseEntity Delete();

    bool DeleteQueued { get; }
}
