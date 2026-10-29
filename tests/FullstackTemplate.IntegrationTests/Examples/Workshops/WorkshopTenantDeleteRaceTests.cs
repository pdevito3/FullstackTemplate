namespace FullstackTemplate.IntegrationTests.Examples.Workshops;

using FullstackTemplate.IntegrationTests.Databases;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Examples.Workshops.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Examples.Fakes;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

/// <summary>
/// A workshop add at the same time as a tenant delete must not leave a live workshop in the deleted tenant. See
/// <c>docs/backend/concurrency.md</c>, section "Lock".
/// </summary>
public class WorkshopTenantDeleteRaceTests : TestBase
{
    [Test]
    public async Task workshop_add_during_a_tenant_delete_waits_and_then_finds_no_venue()
    {
        // Arrange
        var pause = PauseAtQuery.After("tenants.delete.load_workshops");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out var venue)
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var delete = underTest.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.ReachedAsync();
        var add = rival.SendAsync(new AddWorkshop.Command(new FakeWorkshopForCreationDto(venue.Id).Generate()));
        await pause.WaitUntilBlockedAsync(add);
        pause.Release();

        // Assert
        await delete;
        (await Should.ThrowAsync<ValidationException>(add)).Message.ShouldBe("The venue does not exist.");
        (await LiveWorkshopCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Test]
    public async Task tenant_delete_during_a_workshop_add_waits_and_then_deletes_the_new_workshop()
    {
        // Arrange
        // EF tags no insert, so the hold matches its SQL. A save after the work of the lock holds no lock at the insert.
        var pause = PauseAtQuery.Before("INSERT INTO workshops");
        await using var services = pause.BuildServices();
        await new TestingServiceScope().StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = UserRole.Admin().Value)
            .Under(tenant).AddVenue(out var venue)
            .BuildAsync();
        var underTest = new TestingServiceScope(services);
        var rival = new TestingServiceScope();
        underTest.SetUserWithDefaults(identifier: admin.Identifier);
        rival.SetUserWithDefaults(identifier: admin.Identifier);

        // Act
        var add = underTest.SendAsync(new AddWorkshop.Command(new FakeWorkshopForCreationDto(venue.Id).Generate()));
        await pause.ReachedAsync();
        var delete = rival.SendAsync(new DeleteTenant.Command(tenant.Id));
        await pause.WaitUntilBlockedAsync(delete);
        pause.Release();

        // Assert
        var workshop = await add;
        await delete;
        (await new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Workshops.IgnoreQueryFilters().Where(w => w.Id == workshop.Id).Select(w => w.IsDeleted).SingleAsync())).ShouldBeTrue();
        (await LiveWorkshopCountAsync(tenant.Id)).ShouldBe(0);
    }

    private static Task<int> LiveWorkshopCountAsync(string tenantId)
        => new TestingServiceScope().ExecuteDbContextAsync(db =>
            db.Workshops.IgnoreQueryFilters([QueryFilterNames.Tenant]).CountAsync(w => w.TenantId == tenantId));
}
