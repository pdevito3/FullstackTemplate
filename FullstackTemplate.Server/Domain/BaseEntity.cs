namespace FullstackTemplate.Server.Domain;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; private set; } = Guid.NewGuid();
    
    public DateTimeOffset CreatedOn { get; private set; }
    public string CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOn { get; private set; }
    public string LastModifiedBy { get; private set; }
    
    public bool IsDeleted { get; private set; }

    public void UpdateCreationProperties(DateTimeOffset createdOn, string createdBy)
    {
        CreatedOn = createdOn;
        CreatedBy = createdBy;
    }
    
    public void UpdateModifiedProperties(DateTimeOffset? lastModifiedOn, string lastModifiedBy)
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