namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Tenants;
using System.Security.Claims;
using FullstackTemplate.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

public sealed class TenantScopeProviderTests : IDisposable
{
    private const string TrustedClientId = "trusted-machine";
    private const string ReadOnlyClientId = "read-only-machine";
    private const string FirstPartyClientId = "app";

    private readonly ICurrentPrincipalAccessor _principalAccessor = Substitute.For<ICurrentPrincipalAccessor>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly IAgentDirectory _agentDirectory = Substitute.For<IAgentDirectory>();
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly ITenantScopeProvider _sut;

    public TenantScopeProviderTests()
    {
        _agentDirectory.FindDelegatedAgentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _agentDirectory.FindMachineAgentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AgentBinding?)null);

        var services = new ServiceCollection();
        services.AddSingleton(_principalAccessor);
        services.AddSingleton(_tenantIdProvider);
        services.AddSingleton(_agentDirectory);
        services.AddSingleton(Options.Create(new TenancyOptions
        {
            ManageAllTenantsClientIds = [TrustedClientId],
            ReadAllTenantsClientIds = [ReadOnlyClientId]
        }));
        services.AddSingleton(Options.Create(new FirstPartyClientOptions { ClientIds = new HashSet<string> { FirstPartyClientId } }));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<SystemTenantAccess>();
        services.AddScoped<ICallerIdentityResolver, CallerIdentityResolver>();
        services.AddScoped<ITenantScopeProvider, TenantScopeProvider>();
        _services = services.BuildServiceProvider();

        _scope = _services.CreateScope();
        _sut = _scope.ServiceProvider.GetRequiredService<ITenantScopeProvider>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
    }

    private void SetCaller(params Claim[] claims)
    {
        var identity = claims.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(claims, "TestAuth");
        _principalAccessor.Principal.Returns(new ClaimsPrincipal(identity));
    }

    [Test]
    public void user_with_a_user_record_gets_their_tenant()
    {
        // Arrange
        var tenantId = EntityId.New(Tenant.IdPrefix);
        _tenantIdProvider.FindLiveUserAsync("user-1").Returns(new LiveUser("usr_1", tenantId));
        SetCaller(new Claim("sub", "user-1"), new Claim("azp", FirstPartyClientId));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.ForTenant(tenantId));
    }

    [Test]
    public void user_with_no_user_record_gets_no_tenant()
    {
        // Arrange
        _tenantIdProvider.FindLiveUserAsync("user-1").Returns((LiveUser?)null);
        SetCaller(new Claim("sub", "user-1"), new Claim("azp", FirstPartyClientId));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void work_with_no_http_request_and_no_opt_in_gets_no_tenant()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void system_scope_gets_all_tenants()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        using var systemScope = _services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();
        var provider = systemScope.ServiceProvider.GetRequiredService<ITenantScopeProvider>();

        // Act & Assert
        provider.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public void system_scope_does_not_change_other_scopes()
    {
        // Arrange
        _principalAccessor.Principal.Returns((ClaimsPrincipal?)null);

        // Act
        using var systemScope = _services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();

        // Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void read_only_machine_client_gets_all_tenants()
    {
        // Arrange
        SetCaller(new Claim("client_id", ReadOnlyClientId));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public void machine_client_on_the_allow_list_gets_all_tenants()
    {
        // Arrange
        SetCaller(new Claim("client_id", TrustedClientId));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public void service_account_token_on_the_allow_list_gets_all_tenants()
    {
        // Arrange
        // Keycloak, ZeroFactor, and Cognito give a client_credentials token a service-account sub with no user record.
        _tenantIdProvider.FindLiveUserAsync("service-account-1").Returns((LiveUser?)null);
        SetCaller(new Claim("sub", "service-account-1"), new Claim("azp", TrustedClientId));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
    }

    [Test]
    public void user_through_an_unknown_client_gets_no_tenant()
    {
        // Arrange
        _tenantIdProvider.FindLiveUserAsync("user-1").Returns(new LiveUser("usr_1", EntityId.New(Tenant.IdPrefix)));
        SetCaller(new Claim("sub", "user-1"), new Claim("azp", "some-agent"));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void user_through_a_delegated_agent_gets_the_tenant_of_the_user()
    {
        // Arrange
        var tenantId = EntityId.New(Tenant.IdPrefix);
        _tenantIdProvider.FindLiveUserAsync("user-1").Returns(new LiveUser("usr_1", tenantId));
        _agentDirectory.FindDelegatedAgentAsync(tenantId, "clinic-assistant").Returns(EntityId.New(Agent.IdPrefix));
        SetCaller(new Claim("sub", "user-1"), new Claim("azp", "clinic-assistant"));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.ForTenant(tenantId));
    }

    [Test]
    public void machine_agent_gets_the_tenant_of_its_record()
    {
        // Arrange
        var tenantId = EntityId.New(Tenant.IdPrefix);
        _agentDirectory.FindMachineAgentAsync("fleet-sync").Returns(new AgentBinding(EntityId.New(Agent.IdPrefix), tenantId));
        SetCaller(new Claim("client_id", "fleet-sync"));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.ForTenant(tenantId));
    }

    [Test]
    public void machine_client_not_on_the_allow_list_gets_no_tenant()
    {
        // Arrange
        SetCaller(new Claim("client_id", "m2m"));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void allow_list_match_is_case_sensitive()
    {
        // Arrange
        SetCaller(new Claim("client_id", TrustedClientId.ToUpperInvariant()));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void token_with_no_sub_and_no_client_id_gets_no_tenant()
    {
        // Arrange
        SetCaller(new Claim("name", "someone"));

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Test]
    public void anonymous_request_gets_no_tenant()
    {
        // Arrange
        SetCaller();

        // Act & Assert
        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }
}
