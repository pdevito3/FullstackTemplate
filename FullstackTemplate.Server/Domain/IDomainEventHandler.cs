namespace FullstackTemplate.Server.Domain;

using System.Diagnostics.CodeAnalysis;
using Databases;

/// <summary>
/// Runs in the save that commits <typeparamref name="TEvent"/>, before the SQL, so the handler changes commit
/// with the change or nothing commits. See docs/backend/domain-events.md for the rules.
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "A domain event handler is not a .NET event delegate.")]
public interface IDomainEventHandler<TEvent> where TEvent : DomainEvent
{
    /// <param name="domainEvent">The event that an entity queued.</param>
    /// <param name="dbContext">The context of the save. Change entities through it, and do not save.</param>
    /// <param name="cancellationToken">The token of the save.</param>
    Task Handle(TEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken);
}
