namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Shouldly;

public class UpdateTenantTests
{
    [Test]
    public void update_from_a_stale_version_is_refused_and_changes_nothing()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant, t => t.Name = "Before");
        tenant.ClearDomainEvents();

        // Act
        Action act = () => tenant.Update(new TenantForUpdate { Name = "After", Version = tenant.Version + 1 });

        // Assert
        act.ShouldThrow<StaleVersionException>();
        tenant.Name.ShouldBe("Before");
        tenant.DomainEvents.ShouldBeEmpty();
    }

    [Test]
    public void update_from_the_current_version_applies()
    {
        // Arrange
        TestGraph.Start(out _).AddTenant(out var tenant, t => t.Name = "Before");

        // Act
        tenant.Update(new TenantForUpdate { Name = "After", Version = tenant.Version });

        // Assert
        tenant.Name.ShouldBe("After");
        tenant.DomainEvents.OfType<TenantUpdated>().ShouldHaveSingleItem();
    }
}
