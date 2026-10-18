namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using System.Security.Claims;
using FullstackTemplate.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

public sealed class TenantScopeProviderTests : IDisposable
{
    private const string TrustedClientId = "trusted-machine";
    private const string ReadOnlyClientId = "read-only-machine";

    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly ServiceProvider _services;
    private readonly IServiceScope _scope;
    private readonly ITenantScopeProvider _sut;

    public TenantScopeProviderTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_httpContextAccessor);
        services.AddSingleton(_tenantIdProvider);
        services.AddSingleton(Options.Create(new TenancyOptions
        {
            ManageAllTenantsClientIds = [TrustedClientId],
            ReadAllTenantsClientIds = [ReadOnlyClientId]
        }));
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<SystemTenantAccess>();
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
        _httpContextAccessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
    }

    [Fact]
    public void user_with_a_user_record_gets_their_tenant()
    {
        var tenantId = EntityId.New(Tenant.IdPrefix);
        _tenantIdProvider.GetTenantIdAsync("user-1").Returns(tenantId);
        SetCaller(new Claim("sub", "user-1"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.ForTenant(tenantId));
    }

    [Fact]
    public void user_with_no_user_record_gets_no_tenant()
    {
        _tenantIdProvider.GetTenantIdAsync("user-1").Returns((string?)null);
        SetCaller(new Claim("sub", "user-1"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public void work_with_no_http_request_and_no_opt_in_gets_no_tenant()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void system_scope_gets_all_tenants_and_can_manage_them()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        using var systemScope = _services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();
        var provider = systemScope.ServiceProvider.GetRequiredService<ITenantScopeProvider>();

        provider.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
        provider.CanManageAllTenants().ShouldBeTrue();
    }

    [Fact]
    public void system_scope_does_not_change_other_scopes()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        using var systemScope = _services.GetRequiredService<IServiceScopeFactory>().CreateSystemScope();

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void user_with_a_user_record_cannot_manage_all_tenants()
    {
        _tenantIdProvider.GetTenantIdAsync("user-1").Returns(EntityId.New(Tenant.IdPrefix));
        SetCaller(new Claim("sub", "user-1"));

        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void machine_client_on_the_allow_list_can_manage_all_tenants()
    {
        SetCaller(new Claim("client_id", TrustedClientId));

        _sut.CanManageAllTenants().ShouldBeTrue();
    }

    [Fact]
    public void machine_client_not_on_the_allow_list_cannot_manage_all_tenants()
    {
        SetCaller(new Claim("client_id", "m2m"));

        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void anonymous_request_cannot_manage_all_tenants()
    {
        SetCaller();

        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void read_only_machine_client_gets_all_tenants_but_cannot_manage_them()
    {
        SetCaller(new Claim("client_id", ReadOnlyClientId));

        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
        _sut.CanManageAllTenants().ShouldBeFalse();
    }

    [Fact]
    public void machine_client_on_the_allow_list_gets_all_tenants()
    {
        SetCaller(new Claim("client_id", TrustedClientId));

        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
    }

    [Fact]
    public void machine_client_not_on_the_allow_list_gets_no_tenant()
    {
        SetCaller(new Claim("client_id", "m2m"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public void allow_list_match_is_case_sensitive()
    {
        SetCaller(new Claim("client_id", TrustedClientId.ToUpperInvariant()));

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public void token_with_no_sub_and_no_client_id_gets_no_tenant()
    {
        SetCaller(new Claim("name", "someone"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public void anonymous_request_gets_no_tenant()
    {
        SetCaller();

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }
}
