namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;

/// <summary>
/// The rows that a caller sees and the actions that a caller can do are separate decisions.
/// A caller that sees the rows of all tenants cannot add a tenant without the capability.
/// </summary>
public sealed class ManageAllTenantsCapabilityTests : IDisposable
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ITenantScopeProvider _tenantScopeProvider = Substitute.For<ITenantScopeProvider>();
    private readonly AppDbContext _dbContext;

    public ManageAllTenantsCapabilityTests()
    {
        _currentUserService.UserIdentifier.Returns((string?)null);
        _tenantScopeProvider.GetCurrentScope().Returns(TenantScope.AllTenants);
        _tenantScopeProvider.CanManageAllTenants().Returns(false);

        // The access check runs before the handler uses the database, so the context never connects.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        _dbContext = new AppDbContext(options, TimeProvider.System, _currentUserService, _tenantScopeProvider,
            Substitute.For<IMediator>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_add_a_tenant()
    {
        var handler = new AddTenant.Handler(_dbContext, _tenantScopeProvider);

        var act = () => handler.Handle(new AddTenant.Command(new FakeTenantForCreationDto().Generate()),
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_start_a_tenant()
    {
        var handler = new InitiateUserAndTenant.Handler(_dbContext, _currentUserService, _tenantScopeProvider);

        var act = () => handler.Handle(
            new InitiateUserAndTenant.Command(new FakeInitiateUserAndTenantDto().Generate()),
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_add_a_user()
    {
        _currentUserService.IsMachine.Returns(true);
        var permissionGuard = new PermissionGuard(_dbContext, _currentUserService, _tenantScopeProvider);
        var handler = new AddUser.Handler(_dbContext, permissionGuard);

        var act = () => handler.Handle(new AddUser.Command(new FakeUserForCreationDto().Generate()),
            CancellationToken.None);

        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe("This client cannot change data.");
    }
}
