namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class DomainEventTenantTests
{
    [Test]
    public void tenant_of_an_event_from_a_tenant_is_the_tenant()
    {
        // Arrange
        using var dbContext = CreateAppDbContext();
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());

        // Act & Assert
        TenantQueryFilterExtension.TenantIdOfDomainEvent(dbContext.Entry<BaseEntity>(tenant)).ShouldBe(tenant.Id);
    }

    [Test]
    public void tenant_of_an_event_from_a_tenantable_entity_is_its_tenant()
    {
        // Arrange
        using var dbContext = CreateAppDbContext();
        var user = User.Create(new FakeUserForCreation().Generate());

        // Act & Assert
        TenantQueryFilterExtension.TenantIdOfDomainEvent(dbContext.Entry<BaseEntity>(user)).ShouldBe(user.TenantId);
    }

    [Test]
    public void event_from_an_entity_with_no_tenant_has_no_tenant_of_its_own()
    {
        // Arrange
        using var dbContext = new NavigationTenantedContext();
        var memo = new Memo();
        dbContext.Attach(memo);

        // Act & Assert
        TenantQueryFilterExtension.TenantIdOfDomainEvent(dbContext.Entry<BaseEntity>(memo)).ShouldBeNull();
    }

    [Test]
    public void event_from_an_entity_tenanted_through_a_navigation_path_is_refused()
    {
        // Arrange
        using var dbContext = new NavigationTenantedContext();
        var line = new OrderLine(new Order("tnt_owner"));
        dbContext.Attach(line);

        // Act & Assert
        var exception = Should.Throw<NotSupportedException>(
            () => TenantQueryFilterExtension.TenantIdOfDomainEvent(dbContext.Entry<BaseEntity>(line)));

        exception.Message.ShouldContain(nameof(OrderLine));
        exception.Message.ShouldContain("Implement ITenantable");
        exception.Message.ShouldContain("tenanted owner");
    }

    private static AppDbContext CreateAppDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }
}
