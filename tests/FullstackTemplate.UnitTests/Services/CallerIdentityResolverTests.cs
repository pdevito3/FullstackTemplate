namespace FullstackTemplate.UnitTests.Services;

using System.Security.Claims;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

/// <summary>
/// One row for each token shape that a provider gives. The real <see cref="CurrentUserService"/> reads the claims.
/// </summary>
public sealed class CallerIdentityResolverTests : IDisposable
{
    private const string AppClientId = "app";
    private const string SwaggerClientId = "swagger";
    private const string ManageClientId = "tenant-admin-sync";
    private const string ReadClientId = "reporting";
    private const string DelegatedClientId = "clinic-assistant";
    private const string MachineClientId = "fleet-sync";
    private const string AppUser = "user-1";
    private const string ServiceAccount = "service-account-1";

    private static readonly string AppUserTenantId = EntityId.New(Tenant.IdPrefix);
    private static readonly LiveUser AppUserRecord = new(EntityId.New(User.IdPrefix), AppUserTenantId);
    private static readonly string MachineTenantId = EntityId.New(Tenant.IdPrefix);
    private static readonly string DelegatedAgentId = EntityId.New(Agent.IdPrefix);
    private static readonly string MachineAgentId = EntityId.New(Agent.IdPrefix);

    private readonly ICurrentPrincipalAccessor _principalAccessor = Substitute.For<ICurrentPrincipalAccessor>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly IAgentDirectory _agentDirectory = Substitute.For<IAgentDirectory>();
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly ICallerIdentityResolver _sut;

    public CallerIdentityResolverTests()
    {
        _tenantIdProvider.FindLiveUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((LiveUser?)null);
        _tenantIdProvider.FindLiveUserAsync(AppUser, Arg.Any<CancellationToken>()).Returns(AppUserRecord);
        _agentDirectory.FindDelegatedAgentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _agentDirectory.FindDelegatedAgentAsync(AppUserTenantId, DelegatedClientId, Arg.Any<CancellationToken>())
            .Returns(DelegatedAgentId);
        _agentDirectory.FindDelegatedAgentAsync(AppUserTenantId, MachineClientId, Arg.Any<CancellationToken>())
            .Returns(EntityId.New(Agent.IdPrefix));
        _agentDirectory.FindMachineAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AgentBinding?)null);
        _agentDirectory.FindMachineAgentAsync(MachineClientId, Arg.Any<CancellationToken>())
            .Returns(new AgentBinding(MachineAgentId, MachineTenantId));

        var services = new ServiceCollection();
        services.AddSingleton(_principalAccessor);
        services.AddSingleton(_tenantIdProvider);
        services.AddSingleton(_agentDirectory);
        services.AddSingleton(Options.Create(new TenancyOptions
        {
            ManageAllTenantsClientIds = [ManageClientId],
            ReadAllTenantsClientIds = [ReadClientId]
        }));
        services.AddSingleton(Options.Create(new FirstPartyClientOptions
        {
            ClientIds = new HashSet<string> { AppClientId, SwaggerClientId }
        }));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<SystemTenantAccess>();
        services.AddScoped<ICallerIdentityResolver, CallerIdentityResolver>();
        _services = services.BuildServiceProvider();

        _scope = _services.CreateScope();
        _sut = _scope.ServiceProvider.GetRequiredService<ICallerIdentityResolver>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    public static IEnumerable<Func<(string Case, Claim[] Claims, CallerIdentity Expected)>> TokenShapes()
    {
        yield return () => ("first-party user with a user record",
            [new("sub", AppUser), new("azp", AppClientId)],
            new CallerIdentity.User(AppUser, AppUserRecord));
        yield return () => ("first-party user with no user record",
            [new("sub", "new-user"), new("azp", AppClientId)],
            new CallerIdentity.User("new-user", null));
        yield return () => ("Swagger UI user",
            [new("sub", AppUser), new("azp", SwaggerClientId)],
            new CallerIdentity.User(AppUser, AppUserRecord));
        yield return () => ("FusionAuth user, with the client in applicationId",
            [new("sub", AppUser), new("applicationId", AppClientId)],
            new CallerIdentity.User(AppUser, AppUserRecord));
        yield return () => ("first-party client with no sub",
            [new("client_id", AppClientId)],
            new CallerIdentity.UnknownClient(AppClientId));
        yield return () => ("Keycloak service account on the manage list",
            [new("sub", ServiceAccount), new("azp", ManageClientId)],
            new CallerIdentity.AllTenantsClient(ManageClientId, CanManage: true));
        yield return () => ("Duende client_credentials token on the read list",
            [new("client_id", ReadClientId)],
            new CallerIdentity.AllTenantsClient(ReadClientId, CanManage: false));
        yield return () => ("client_credentials token on no list",
            [new("client_id", "m2m")],
            new CallerIdentity.UnknownClient("m2m"));
        yield return () => ("unknown client with an app user sub",
            [new("sub", AppUser), new("azp", "claude-desktop")],
            new CallerIdentity.UnknownClient("claude-desktop"));
        yield return () => ("client on the manage list with the sub of an app user",
            [new("sub", AppUser), new("azp", ManageClientId)],
            new CallerIdentity.AllTenantsClient(ManageClientId, CanManage: true));
        yield return () => ("client on the read list with the sub of an app user",
            [new("sub", AppUser), new("azp", ReadClientId)],
            new CallerIdentity.AllTenantsClient(ReadClientId, CanManage: false));
        yield return () => ("app user with no client claim",
            [new("sub", AppUser)],
            new CallerIdentity.UnknownClient(null));
        yield return () => ("app user through a delegated agent of their tenant",
            [new("sub", AppUser), new("azp", DelegatedClientId)],
            new CallerIdentity.DelegatedAgent(DelegatedAgentId, DelegatedClientId, AppUser, AppUserRecord));
        yield return () => ("delegated agent client with no app user",
            [new("client_id", DelegatedClientId)],
            new CallerIdentity.UnknownClient(DelegatedClientId));
        yield return () => ("Keycloak service account of a machine agent",
            [new("sub", ServiceAccount), new("azp", MachineClientId)],
            new CallerIdentity.MachineAgent(MachineAgentId, MachineClientId, MachineTenantId));
        yield return () => ("Duende client_credentials token of a machine agent",
            [new("client_id", MachineClientId)],
            new CallerIdentity.MachineAgent(MachineAgentId, MachineClientId, MachineTenantId));
        yield return () => ("machine agent client with the sub of an app user whose tenant registered it as delegated",
            [new("sub", AppUser), new("azp", MachineClientId)],
            new CallerIdentity.MachineAgent(MachineAgentId, MachineClientId, MachineTenantId));
        yield return () => ("client_id wins over azp",
            [new("sub", AppUser), new("client_id", "claude-desktop"), new("azp", AppClientId)],
            new CallerIdentity.UnknownClient("claude-desktop"));
    }

    [Test]
    [MethodDataSource(nameof(TokenShapes))]
    public async Task each_token_shape_resolves_to_its_identity(string @case, Claim[] claims, CallerIdentity expected)
    {
        // Arrange
        SetCaller(claims);

        // Act
        var current = _sut.Current;
        var awaited = await _sut.GetAsync(CancellationToken.None);

        // Assert
        current.ShouldBe(expected);
        awaited.ShouldBe(expected);
    }

    [Test]
    public void request_with_no_authenticated_principal_is_anonymous()
    {
        // Arrange
        _principalAccessor.Principal.Returns(new ClaimsPrincipal(new ClaimsIdentity()));

        // Act & Assert
        _sut.Current.ShouldBe(new CallerIdentity.Anonymous());
    }

    [Test]
    public void work_with_no_request_is_anonymous()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act & Assert
        _sut.Current.ShouldBe(new CallerIdentity.Anonymous());
    }

    [Test]
    public void system_scope_is_system_work_whatever_the_token()
    {
        // Arrange
        SetCaller(new Claim("sub", AppUser), new Claim("azp", AppClientId));
        using var systemScope = _services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();

        // Act
        var identity = systemScope.ServiceProvider.GetRequiredService<ICallerIdentityResolver>().Current;

        // Assert
        identity.ShouldBe(new CallerIdentity.System());
    }

    [Test]
    public void the_tenant_of_a_user_is_looked_up_once_for_each_caller()
    {
        // Arrange
        SetCaller(new Claim("sub", AppUser), new Claim("azp", AppClientId));

        // Act
        _ = _sut.Current;
        _ = _sut.Current;

        // Assert
        _tenantIdProvider.Received(1).FindLiveUserAsync(AppUser, Arg.Any<CancellationToken>());
    }

    [Test]
    public void a_new_caller_in_the_same_scope_gets_a_new_identity()
    {
        // Arrange
        SetCaller(new Claim("sub", AppUser), new Claim("azp", AppClientId));
        _ = _sut.Current;

        // Act
        SetCaller(new Claim("client_id", ManageClientId));

        // Assert
        _sut.Current.ShouldBe(new CallerIdentity.AllTenantsClient(ManageClientId, CanManage: true));
    }

    private void SetCaller(params Claim[] claims)
        => _principalAccessor.Principal.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
}
