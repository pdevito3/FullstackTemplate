namespace FullstackTemplate.IntegrationTests.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.DomainEvents;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

public class DomainEventTenantTests : TestBase
{
    private sealed record HandlerView(TenantScope Scope, List<string> UserTenantIds, List<string> TenantIds);

    [Test]
    public async Task handler_sees_only_the_tenant_of_the_event_for_a_machine_client_that_sees_all_tenants()
    {
        // Arrange
        var views = new List<HandlerView>();
        await using var services = BuildServicesThatRecord(views);
        var scope = new TestingServiceScope(services);
        var scenario = await scope.StartTenantScenarioAsync();
        scope.SetMachineClient(TestFixture.TrustedMachineClientId);
        var db = scope.GetService<AppDbContext>();

        // Act
        await RenameTenantAsync(db, scenario.Tenant.Id);

        // Assert
        views.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            view => view.Scope.ShouldBe(TenantScope.ForTenant(scenario.Tenant.Id)),
            view => view.UserTenantIds.ShouldBe([scenario.Tenant.Id]),
            view => view.TenantIds.ShouldBe([scenario.Tenant.Id]));
        db.CurrentTenantScope.ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public async Task handler_sees_only_the_tenant_of_the_event_in_a_system_scope()
    {
        // Arrange
        var views = new List<HandlerView>();
        await using var services = BuildServicesThatRecord(views);
        var scenario = await new TestingServiceScope(services).StartTenantScenarioAsync();
        await using var system = services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();
        var db = system.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        await RenameTenantAsync(db, scenario.Tenant.Id);

        // Assert
        views.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            view => view.Scope.ShouldBe(TenantScope.ForTenant(scenario.Tenant.Id)),
            view => view.UserTenantIds.ShouldBe([scenario.Tenant.Id]),
            view => view.TenantIds.ShouldBe([scenario.Tenant.Id]));
        db.CurrentTenantScope.ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public async Task tenant_created_handler_runs_in_the_new_tenant_of_a_new_user()
    {
        // Arrange
        var scopes = new List<TenantScope>();
        await using var services = TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantCreated>((_, db, _) =>
        {
            scopes.Add(db.CurrentTenantScope);
            return Task.CompletedTask;
        }));
        var scope = new TestingServiceScope(services);
        var dto = new FakeInitiateUserAndTenantDto().Generate();
        scope.SetUserWithDefaults(identifier: dto.Identifier);

        // Act
        var result = await scope.SendAsync(new InitiateUserAndTenant.Command(dto));

        // Assert
        scopes.ShouldBe([TenantScope.ForTenant(result.Tenant.Id)]);
    }

    private static ServiceProvider BuildServicesThatRecord(List<HandlerView> views)
        => TestFixture.BuildServiceProvider(s => s.AddTestHandler<TenantUpdated>(async (_, db, ct) =>
        {
            var scope = db.CurrentTenantScope;
            var userTenantIds = await db.Users.Select(u => u.TenantId).Distinct().ToListAsync(ct);
            var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync(ct);
            views.Add(new HandlerView(scope, userTenantIds, tenantIds));
        }));

    private static async Task RenameTenantAsync(AppDbContext db, string tenantId)
    {
        (await db.Tenants.CountAsync()).ShouldBeGreaterThan(1);
        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.Update(new TenantForUpdate { Name = $"renamed-{Guid.NewGuid()}" });
        await db.SaveChangesAsync();
    }
}
