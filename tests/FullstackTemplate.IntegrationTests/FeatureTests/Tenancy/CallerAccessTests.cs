namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class CallerAccessTests : TestBase
{
    [Test]
    public async Task unauthenticated_caller_sees_no_rows()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();
        testingServiceScope.SetUserUnauthenticated();

        // Act
        var userFound = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == user.Id));
        var tenantFound = await testingServiceScope.ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == user.TenantId));

        // Assert
        userFound.ShouldBeFalse();
        tenantFound.ShouldBeFalse();
    }

    [Test]
    public async Task unauthenticated_caller_cannot_add_a_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetUserUnauthenticated();

        // Act
        var act = () => testingServiceScope.SendAsync(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task unauthenticated_caller_cannot_start_a_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetUserUnauthenticated();

        // Act
        var act = () => testingServiceScope.SendAsync(
            new InitiateUserAndTenant.Command(new FakeInitiateUserAndTenantDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task machine_client_not_on_the_allow_list_sees_no_rows()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();
        testingServiceScope.SetMachineClient("untrusted-machine");

        // Act
        var userFound = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == user.Id));

        // Assert
        userFound.ShouldBeFalse();
    }

    [Test]
    public async Task machine_client_not_on_the_allow_list_cannot_add_a_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient("untrusted-machine");

        // Act
        var act = () => testingServiceScope.SendAsync(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task machine_client_not_on_the_allow_list_cannot_start_a_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient("untrusted-machine");

        // Act
        var act = () => testingServiceScope.SendAsync(
            new InitiateUserAndTenant.Command(new FakeInitiateUserAndTenantDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task machine_client_on_the_allow_list_sees_all_tenants_and_can_add_a_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var otherTenant)
            .AddUser(out var otherUser)
            .BuildAsync();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);

        // Act
        var userFound = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == otherUser.Id));
        var tenantFound = await testingServiceScope.ExecuteDbContextAsync(db => db.Tenants.AnyAsync(t => t.Id == otherTenant.Id));
        var added = await testingServiceScope.SendAsync(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        userFound.ShouldBeTrue();
        tenantFound.ShouldBeTrue();
        EntityId.IsValid(added.Id, Tenant.IdPrefix).ShouldBeTrue();
    }
}
