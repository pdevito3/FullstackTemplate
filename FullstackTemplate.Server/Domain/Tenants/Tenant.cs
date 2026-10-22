namespace FullstackTemplate.Server.Domain.Tenants;

using DomainEvents;
using Models;

public class Tenant : BaseEntity
{
    public const string IdPrefix = "ten";

    public string Name { get; private set; } = default!;

    public static Tenant Create(TenantForCreation tenantForCreation)
    {
        var tenant = new Tenant
        {
            Name = tenantForCreation.Name
        };
        if (tenantForCreation.Id is not null)
            tenant.SetId(tenantForCreation.Id);

        ValidateTenant(tenant);
        tenant.QueueDomainEvent(new TenantCreated(tenant.Id));

        return tenant;
    }

    public Tenant Update(TenantForUpdate tenantForUpdate)
    {
        GuardVersion(tenantForUpdate.Version);
        Name = tenantForUpdate.Name;

        ValidateTenant(this);
        QueueDomainEvent(new TenantUpdated(Id));

        return this;
    }

    private static void ValidateTenant(Tenant tenant)
    {
        Exceptions.ValidationException.ThrowWhenNullOrWhitespace(tenant.Name, "name", "Please provide a tenant name.");
    }

    protected Tenant() : base(IdPrefix) { } // For EF Core
}
