namespace FullstackTemplate.UnitTests.Services;

using System.Security.Claims;
using FullstackTemplate.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

public class TenantScopeProviderTests
{
    private const string TrustedClientId = "trusted-machine";

    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();
    private readonly ITenantIdProvider _tenantIdProvider = Substitute.For<ITenantIdProvider>();
    private readonly TenantScopeProvider _sut;

    public TenantScopeProviderTests()
    {
        var options = Options.Create(new TenancyOptions { AllTenantClientIds = [TrustedClientId] });
        _sut = new TenantScopeProvider(new CurrentUserService(_httpContextAccessor), _tenantIdProvider, options);
    }

    private void SetCaller(params Claim[] claims)
    {
        var identity = claims.Length == 0 ? new ClaimsIdentity() : new ClaimsIdentity(claims, "TestAuth");
        _httpContextAccessor.HttpContext.Returns(new DefaultHttpContext { User = new ClaimsPrincipal(identity) });
    }

    [Fact]
    public void user_with_a_user_record_gets_their_tenant()
    {
        var tenantId = Guid.NewGuid();
        _tenantIdProvider.GetTenantIdAsync("user-1").Returns(tenantId);
        SetCaller(new Claim("sub", "user-1"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.ForTenant(tenantId));
    }

    [Fact]
    public void user_with_no_user_record_gets_no_tenant()
    {
        _tenantIdProvider.GetTenantIdAsync("user-1").Returns((Guid?)null);
        SetCaller(new Claim("sub", "user-1"));

        _sut.GetCurrentScope().ShouldBe(TenantScope.NoTenant);
    }

    [Fact]
    public void work_with_no_http_request_gets_all_tenants()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);

        _sut.GetCurrentScope().ShouldBe(TenantScope.AllTenants);
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
