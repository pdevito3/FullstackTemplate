namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class InitiateUserAndTenantCommandTests : TestBase
{
    [Fact]
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

    [Fact]
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

    [Fact]
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
}
