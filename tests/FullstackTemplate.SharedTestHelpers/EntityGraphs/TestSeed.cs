namespace FullstackTemplate.SharedTestHelpers.EntityGraphs;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;

/// <summary>
/// The baseline that every scenario starts from: one tenant and the current user in that tenant.
/// Integration tests insert one seed per test run. Unit tests generate a new seed per test and never save it.
/// </summary>
public sealed record TestSeed(Tenant Tenant, User User)
{
    public static TestSeed Generate()
    {
        var tenant = Tenant.Create(new FakeTenantForCreation().Generate());
        var user = User.Create(new FakeUserForCreation().Generate() with { TenantId = tenant.Id });
        return new TestSeed(tenant, user);
    }
}
