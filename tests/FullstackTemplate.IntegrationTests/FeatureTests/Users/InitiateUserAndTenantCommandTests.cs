namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class InitiateUserAndTenantCommandTests : TestBase
{
    [Test]
    public async Task user_with_no_user_record_can_start_a_tenant_for_themselves()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeInitiateUserAndTenantDto().Generate();
        testingServiceScope.SetUserWithDefaults(identifier: dto.Identifier);

        // Act
        var result = await testingServiceScope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        var userInDb = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == result.User.Id));
        userInDb.ShouldNotBeNull();
        userInDb.Identifier.ShouldBe(dto.Identifier);
        userInDb.TenantId.ShouldBe(result.Tenant.Id);
    }

    [Test]
    public async Task new_admin_gets_each_action_for_their_own_record()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeInitiateUserAndTenantDto().Generate() with { Role = "Admin" };
        testingServiceScope.SetUserWithDefaults(identifier: dto.Identifier);

        // Act
        var result = await testingServiceScope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        result.User.AllowedActions.ShouldBe(["Update", "UpdateRole", "AddPermission", "RemovePermission", "Delete"]);
    }

    [Test]
    public async Task user_cannot_start_a_tenant_for_another_identifier()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetUserWithDefaults(identifier: $"unknown-{Guid.NewGuid()}");
        var dto = new FakeInitiateUserAndTenantDto().Generate();

        // Act
        var act = () => testingServiceScope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task user_with_a_user_record_cannot_start_another_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        var dto = new FakeInitiateUserAndTenantDto()
            .RuleFor(x => x.Identifier, _ => TestFixture.DefaultUser.Identifier)
            .Generate();

        // Act
        var act = () => testingServiceScope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        var exception = await act.ShouldThrowAsync<ConflictException>();
        exception.Message.ShouldBe("A user with this identifier already exists.");
    }

    [Test]
    public async Task user_of_a_deleted_tenant_can_start_a_new_tenant()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        testingServiceScope.SetMachineClient(TestFixture.TrustedMachineClientId);
        await testingServiceScope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user)
            .BuildAsync();
        await testingServiceScope.SendAsync(new DeleteTenant.Command(tenant.Id));
        testingServiceScope.SetUserWithDefaults(identifier: user.Identifier);
        var dto = new FakeInitiateUserAndTenantDto()
            .RuleFor(x => x.Identifier, _ => user.Identifier)
            .Generate();

        // Act
        var result = await testingServiceScope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        result.Tenant.Id.ShouldNotBe(tenant.Id);
        result.User.Id.ShouldNotBe(user.Id);
        result.User.TenantId.ShouldBe(result.Tenant.Id);
        var oldUser = await testingServiceScope.ExecuteDbContextAsync(db =>
            db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == user.Id));
        oldUser.IsDeleted.ShouldBeTrue();
        oldUser.TenantId.ShouldBe(tenant.Id);
    }
}
