namespace FullstackTemplate.UnitTests.Domain.Tenants;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using Shouldly;

public class ScopeGrantTests
{
    private static readonly string TenantId = EntityId.New(Tenant.IdPrefix);

    public static IEnumerable<Func<(string Case, Action<ScopeGrant> First, string Granted)>> FirstGrants()
    {
        yield return () => ("system work", grant => grant.GrantSystem(), nameof(CallerIdentity.System));
        yield return () => ("job of a tenant", grant => grant.GrantTenantJob(TenantId), nameof(CallerIdentity.TenantJob));
    }

    [Test]
    [MethodDataSource(nameof(FirstGrants))]
    public void second_grant_in_one_scope_is_refused_and_the_first_stays(
        string @case, Action<ScopeGrant> first, string granted)
    {
        // Arrange
        var grant = new ScopeGrant();
        first(grant);
        var identity = grant.Identity;

        // Act
        var act = () => grant.GrantTenantJob(EntityId.New(Tenant.IdPrefix));

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message
            .ShouldBe($"This DI scope already runs as {granted}. A scope gets one identity.");
        grant.Identity.ShouldBe(identity);
    }

    [Test]
    public void grant_of_a_job_names_the_tenant_of_the_job()
    {
        // Arrange
        var grant = new ScopeGrant();

        // Act
        grant.GrantTenantJob(TenantId);

        // Assert
        grant.Identity.ShouldBe(new CallerIdentity.TenantJob(TenantId));
    }
}
