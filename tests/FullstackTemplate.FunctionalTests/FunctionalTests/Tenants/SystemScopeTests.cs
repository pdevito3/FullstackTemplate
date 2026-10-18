namespace FullstackTemplate.FunctionalTests.FunctionalTests.Tenants;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

/// <summary>
/// Work with no HTTP request, such as fire-and-forget work that lost its request, gets no tenant
/// unless it opts in with <see cref="SystemScopeExtensions.CreateSystemScope"/>.
/// </summary>
public class SystemScopeTests : TestBase
{
    private static IServiceScopeFactory ScopeFactory =>
        TestingWebApplicationFactory.Instance.Services.GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public async Task scope_with_no_http_request_and_no_opt_in_sees_no_tenant()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        using var scope = ScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Act
        var found = await dbContext.Tenants.AnyAsync(t => t.Id == tenant.Id);

        // Assert
        found.ShouldBeFalse();
        dbContext.CurrentTenantScope.ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public async Task scope_with_no_http_request_and_no_opt_in_cannot_add_a_tenant()
    {
        // Arrange
        using var scope = ScopeFactory.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Act
        var act = () => sender.Send(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task system_scope_sees_all_tenants_and_can_add_a_tenant()
    {
        // Arrange
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        await InsertAsync(tenant);
        await using var scope = ScopeFactory.CreateSystemScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        // Act
        var found = await dbContext.Tenants.AnyAsync(t => t.Id == tenant.Id);
        var added = await sender.Send(new AddTenant.Command(new FakeTenantForCreationDto().Generate()));

        // Assert
        found.ShouldBeTrue();
        added.Id.ShouldNotBe(Guid.Empty);
    }
}
