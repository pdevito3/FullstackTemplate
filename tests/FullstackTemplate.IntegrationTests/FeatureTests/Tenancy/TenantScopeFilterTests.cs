namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using Microsoft.EntityFrameworkCore;
using Shouldly;

public class TenantScopeFilterTests : TestBase
{
    [Test]
    public async Task user_with_no_user_record_sees_no_row_even_when_the_row_has_an_empty_tenant_id()
    {
        // Arrange
        var testingServiceScope = new TestingServiceScope();
        await testingServiceScope.StartGraph(out _)
            .AddUser(out var user)
            .BuildAsync();

        // The domain does not let a row have an empty tenant ID. The filter must deny without this rule,
        // so the test writes the row directly. The replica role turns off the foreign key trigger.
        await TestFixture.ExecuteOutOfBandAsync(
            "SET session_replication_role = replica; UPDATE users SET tenant_id = @tenantId WHERE id = @id; SET session_replication_role = origin;",
            ("tenantId", string.Empty), ("id", user.Id));

        try
        {
            testingServiceScope.SetUserWithDefaults(identifier: $"unknown-{Guid.NewGuid()}");

            // Act
            var users = await testingServiceScope.SendAsync(new GetUserList.Query(new UserParametersDto { PageSize = 500 }));
            var found = await testingServiceScope.ExecuteDbContextAsync(db => db.Users.AnyAsync(u => u.Id == user.Id));

            // Assert
            users.ShouldBeEmpty();
            found.ShouldBeFalse();
        }
        finally
        {
            await TestFixture.ExecuteOutOfBandAsync("DELETE FROM users WHERE id = @id", ("id", user.Id));
        }
    }
}
