namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

/// <summary>
/// The rows that a caller sees and the actions that a caller can do are separate decisions.
/// A caller that sees the rows of all tenants cannot add a tenant without the capability.
/// </summary>
public sealed class ManageAllTenantsCapabilityTests : IDisposable
{
    private readonly ICallerIdentityResolver _callerIdentityResolver = Substitute.For<ICallerIdentityResolver>();
    private readonly TenantScopeProvider _tenantScopeProvider;
    private readonly AppDbContext _dbContext;

    public ManageAllTenantsCapabilityTests()
    {
        var readOnlyClient = new CallerIdentity.AllTenantsClient("read-only-machine", CanManage: false);
        _callerIdentityResolver.Current.Returns(readOnlyClient);
        _callerIdentityResolver.GetAsync(Arg.Any<CancellationToken>()).Returns(readOnlyClient);
        _tenantScopeProvider = new TenantScopeProvider(_callerIdentityResolver);

        // The access check runs before the handler uses the database, so the context never connects.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        _dbContext = new AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            _tenantScopeProvider, new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()));
    }

    public void Dispose() => _dbContext.Dispose();

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_add_a_tenant()
    {
        var handler = new AddTenant.Handler(_dbContext, new PermissionGuard(_dbContext, _callerIdentityResolver));

        var act = () => handler.Handle(new AddTenant.Command(new FakeTenantForCreationDto().Generate()),
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_start_a_tenant()
    {
        var handler = new InitiateUserAndTenant.Handler(_dbContext, _callerIdentityResolver);

        var act = () => handler.Handle(
            new InitiateUserAndTenant.Command(new FakeInitiateUserAndTenantDto().Generate()),
            CancellationToken.None);

        await act.ShouldThrowAsync<ForbiddenAccessException>();
    }

    [Test]
    public async Task caller_that_sees_all_tenants_without_the_capability_cannot_add_a_user()
    {
        var permissionGuard = new PermissionGuard(_dbContext, _callerIdentityResolver);
        var handler = new AddUser.Handler(_dbContext, permissionGuard);

        var act = () => handler.Handle(new AddUser.Command(new FakeUserForCreationDto().Generate()),
            CancellationToken.None);

        (await act.ShouldThrowAsync<ForbiddenAccessException>()).Message.ShouldBe("This client cannot change data.");
    }
}
