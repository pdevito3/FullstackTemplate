namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using static FullstackTemplate.IntegrationTests.DomainEvents.DomainEventTestServices;

public class LostCommitReplyTests : TestBase
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task update_that_loses_the_connection_at_the_commit_saves_once_and_returns_the_saved_version(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failCommit)));
        var scope = new TestingServiceScope(services);
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUser(out var admin, u => u.Role = "Admin")
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: admin.Identifier);
        var loaded = await scope.SendAsync(new GetTenant.Query(tenant.Id));
        var newName = $"Tenant {Guid.NewGuid():N}";
        failCommit.Arm();

        // Act
        var updated = await scope.SendAsync(new UpdateTenant.Command(tenant.Id,
            new TenantForUpdateDto { Name = newName, Version = loaded.Version }));

        // Assert
        failCommit.Failures.ShouldBe(1);
        updated.Name.ShouldBe(newName);
        var saved = await ReadAsync(read => read.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == tenant.Id));
        saved.Name.ShouldBe(newName);
        updated.Version.ShouldBe(saved.Version);
        (await ReadAsync(read => read.AuditEntries.IgnoreQueryFilters()
            .CountAsync(e => e.RootId == tenant.Id && e.RequestName == "UpdateTenant.Command")))
            .ShouldBe(1);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task insert_that_loses_the_connection_at_the_commit_saves_once(bool afterCommit)
    {
        // Arrange
        var failCommit = new FailFirstCommit(afterCommit);
        await using var services = TestFixture.BuildServiceProvider(s => s
            .ConfigureDbContext<AppDbContext>((_, options) => options.AddInterceptors(failCommit)));
        var scope = new TestingServiceScope(services);
        scope.SetMachineClient(TestFixture.TrustedMachineClientId);
        failCommit.Arm();

        // Act
        var added = await scope.SendAsync(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        failCommit.Failures.ShouldBe(1);
        var saved = await ReadAsync(read => read.Tenants.IgnoreQueryFilters().Where(t => t.Id == added.Id).ToListAsync());
        saved.Count.ShouldBe(1);
        added.Version.ShouldBe(saved[0].Version);
        (await ReadAsync(read => read.AuditEntries.IgnoreQueryFilters().CountAsync(e => e.RootId == added.Id)))
            .ShouldBe(1);
    }
}
