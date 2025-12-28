namespace FullstackTemplate.Server.Domain;

using System.ComponentModel.DataAnnotations.Schema;

public abstract class BaseEntity
{

    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateTimeOffset CreatedOn { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOn { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }

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
    
    public void OverrideId(Guid id)
    {
        Id = id;
    }
}