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

    public DateTimeOffset CreatedOn { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOn { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>
    /// The Postgres <c>xmin</c> of the row, which changes on each write. It is opaque: compare it only for equality.
    /// It is 0 before the first save.
    /// </summary>
    public uint Version { get; private set; }

    private readonly List<DomainEvent> _domainEvents = [];
    [NotMapped]
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void QueueDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public void UpdateCreationProperties(DateTimeOffset createdOn, string? createdBy)
    {
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }

    public void UpdateModifiedProperties(DateTimeOffset? lastModifiedOn, string? lastModifiedBy)
    {
        LastModifiedOn = lastModifiedOn;
        LastModifiedBy = lastModifiedBy;
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
            $"The id '{id}' must start with '{_idPrefix}_', then have only lower-case letters and digits, and have no more than {EntityId.MaxLength} characters.");
        _id = id;
    }
}