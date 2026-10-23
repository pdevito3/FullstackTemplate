namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using Shouldly;

public class TenantDomainEventTests
{
    [Test]
    public void tenant_creation_queues_the_created_event_with_the_tenant_id()
    {
        // Act
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());

        // Assert
        tenant.DomainEvents.ShouldBe([new TenantCreated(tenant.Id)]);
    }

    [Test]
    public void tenant_update_queues_the_updated_event_with_the_tenant_id()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());

        // Act
        tenant.Update(new TenantForUpdate { Name = "Renamed" });

        // Assert
        tenant.DomainEvents.ShouldBe([new TenantCreated(tenant.Id), new TenantUpdated(tenant.Id)]);
    }
}
