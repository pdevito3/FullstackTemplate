namespace FullstackTemplate.Server.Domain.Tenants;

using DomainEvents;
using Models;

public class Tenant : BaseEntity
{
    public string Name { get; private set; } = default!;

    public static Tenant Create(TenantForCreation tenantForCreation)
    {
        var tenant = new Tenant
        {
            Name = tenantForCreation.Name
        };

        ValidateTenant(tenant);
        tenant.QueueDomainEvent(new TenantCreated(tenant));

        return tenant;
    }

    public Tenant Update(TenantForUpdate tenantForUpdate)
    {
        Name = tenantForUpdate.Name;

        ValidateTenant(this);
        QueueDomainEvent(new TenantUpdated(Id));

        return this;
    }

    private static void ValidateTenant(Tenant tenant)
    {
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(tenant.Name, "Please provide a tenant name.");
    }

    protected Tenant() { } // For EF Core
}
