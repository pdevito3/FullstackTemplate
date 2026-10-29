namespace FullstackTemplate.Server.Domain;

using System.ComponentModel.DataAnnotations.Schema;

public abstract class BaseEntity
{
    private readonly string _idPrefix;

    // The ID is lazy, because EF uses the constructor when it loads a row and then writes this field.
    // A new ID made in the constructor would be thrown away for each loaded row.
    private string? _id;

    /// <summary>The name of the backing field of <see cref="Id"/>. EF writes this field when it loads a row.</summary>
    internal const string IdField = nameof(_id);

    /// <param name="idPrefix">The short type prefix of the ID, for example "usr". Each entity gives it as a constant.</param>
    protected BaseEntity(string idPrefix)
    {
        _idPrefix = idPrefix;
    }

    /// <summary>A new entity makes its ID the first time that code reads it, so the ID is known before the save.</summary>
    public string Id => _id ??= EntityId.New(_idPrefix);

    /// <summary>
    /// The time of the last save that wrote the row. The audit log records who changed what and when. This column
    /// stays because the save marks it modified on an unchanged owner of a changed child, so that EF writes the owner
    /// row and its <see cref="Version"/> changes (ADR 0008).
    /// </summary>
    public DateTimeOffset? LastModifiedOn { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>
    /// The Postgres <c>xmin</c> of the row, which changes on each write. It is opaque: compare it only for equality.
    /// It is 0 before the first save.
    /// </summary>
    public uint Version { get; private set; }

    private static long _lastDomainEventSequence;
    private List<QueuedDomainEvent> _domainEvents = [];

    /// <summary>The events that this entity queued and that no save has taken yet.</summary>
    [NotMapped]
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.Select(queued => queued.Event).ToList();

    /// <summary>Queues an event for the next save, which runs its handlers before it commits.</summary>
    protected void QueueDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(new QueuedDomainEvent(Interlocked.Increment(ref _lastDomainEventSequence), domainEvent));
    }

    internal IReadOnlyList<QueuedDomainEvent> TakeDomainEvents()
    {
        var taken = _domainEvents;
        _domainEvents = [];
        return taken;
    }

    public void UpdateLastModifiedOn(DateTimeOffset lastModifiedOn)
    {
        LastModifiedOn = lastModifiedOn;
    }
    
    public void UpdateIsDeleted(bool isDeleted)
    {
        IsDeleted = isDeleted;
    }
    
    protected void GuardVersion(uint expectedVersion)
    {
        if (expectedVersion != Version)
            throw new Exceptions.StaleVersionException();
    }

    /// <summary>Replaces the generated ID. The new ID must have the prefix of this entity type.</summary>
    protected void SetId(string id)
    {
        Exceptions.ValidationException.Must(EntityId.IsValid(id, _idPrefix), "id",
            $"The ID must start with '{_idPrefix}_', then have only lower-case letters and digits, and have no more than {EntityId.MaxLength} characters.");
        _id = id;
    }
}