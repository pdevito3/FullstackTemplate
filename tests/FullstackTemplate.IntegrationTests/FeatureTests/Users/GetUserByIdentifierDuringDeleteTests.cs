namespace FullstackTemplate.IntegrationTests.FeatureTests.Users;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Shouldly;

/// <summary>
/// A delete can commit while a by-identifier request runs. A user who reads their own identifier gets the 404 that the
/// next request gets. A read of any other identifier keeps its permission check, so the answer does not tell which
/// identifiers exist.
/// </summary>
public class GetUserByIdentifierDuringDeleteTests : TestBase
{
    private const string BeforeRecordRead = "users.get_by_identifier";
    private const string BeforeCallerLoad = "users.permission_guard.load";

    [Test]
    [Arguments(BeforeRecordRead)]
    [Arguments(BeforeCallerLoad)]
    public async Task user_who_reads_their_own_identifier_during_the_delete_of_their_record_gets_not_found(string step)
    {
        // Arrange
        var (admin, member, _) = await AddTenantAsync();

        // Act
        var error = await ReadDuringDeleteAsync<NotFoundException>(step, member, member.Identifier,
            rival => rival.SendAsync(new DeleteUser.Command(member.Id)), admin);

        // Assert
        error.Message.ShouldBe("User was not found.");
    }

    [Test]
    [Arguments(BeforeRecordRead)]
    [Arguments(BeforeCallerLoad)]
    public async Task user_who_reads_their_own_identifier_during_the_delete_of_their_tenant_gets_not_found(string step)
    {
        // Arrange
        var (admin, member, tenantId) = await AddTenantAsync();

        // Act
        var error = await ReadDuringDeleteAsync<NotFoundException>(step, member, member.Identifier,
            rival => rival.SendAsync(new DeleteTenant.Command(tenantId)), admin);

        // Assert
        error.Message.ShouldBe("User was not found.");
    }

    [Test]
    public async Task member_who_reads_another_identifier_during_the_delete_of_that_user_is_forbidden()
    {
        // Arrange
        var (admin, member, other) = await AddTenantWithOtherMemberAsync();

        // Act
        var error = await ReadDuringDeleteAsync<ForbiddenAccessException>(BeforeRecordRead, member, other.Identifier,
            rival => rival.SendAsync(new DeleteUser.Command(other.Id)), admin);

        // Assert
        error.Message.ShouldBe("You cannot see the users of the tenant. Ask a tenant admin for access.");
    }

    [Test]
    public async Task user_who_reads_another_identifier_during_the_delete_of_their_record_is_forbidden()
    {
        // Arrange
        var (admin, member, other) = await AddTenantWithOtherMemberAsync();

        // Act
        var error = await ReadDuringDeleteAsync<ForbiddenAccessException>(BeforeCallerLoad, member, $"unknown-{other.Id}",
            rival => rival.SendAsync(new DeleteUser.Command(member.Id)), admin);

        // Assert
        error.Message.ShouldBe("You are not a member of a tenant. Start a tenant to become its admin, or ask the admin of a tenant to add you.");
    }

    // The compare is exact. A variant of the own identifier is another identifier, so it keeps the permission check.
    [Test]
    [Arguments("upper case")]
    [Arguments("spaces")]
    public async Task user_who_reads_a_variant_of_their_own_identifier_during_the_delete_of_their_record_is_forbidden(string variant)
    {
        // Arrange
        var (admin, member, _) = await AddTenantAsync();
        var identifier = variant == "spaces" ? $" {member.Identifier} " : member.Identifier.ToUpperInvariant();

        // Act
        var error = await ReadDuringDeleteAsync<ForbiddenAccessException>(BeforeCallerLoad, member, identifier,
            rival => rival.SendAsync(new DeleteUser.Command(member.Id)), admin);

        // Assert
        error.Message.ShouldBe("You are not a member of a tenant. Start a tenant to become its admin, or ask the admin of a tenant to add you.");
    }

    private static async Task<TException> ReadDuringDeleteAsync<TException>(
        string step, User caller, string identifier, Func<TestingServiceScope, Task> delete, User admin)
        where TException : Exception
    {
        var pause = PauseAtQuery.Before(step);
        await using var services = pause.BuildServices();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: caller.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        var read = underTest.SendAsync(new GetUserByIdentifier.Query(identifier));
        await pause.ReachedAsync();
        await delete(rival);
        pause.Release();

        return await Should.ThrowAsync<TException>(read);
    }

    private static async Task<(User Admin, User Member, string TenantId)> AddTenantAsync()
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User")
            .BuildAsync();
        return (users[0], users[1], tenant.Id);
    }

    private static async Task<(User Admin, User Member, User Other)> AddTenantWithOtherMemberAsync()
    {
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out _)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User", u => u.Role = "User")
            .BuildAsync();
        return (users[0], users[1], users[2]);
    }
}
