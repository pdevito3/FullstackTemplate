namespace FullstackTemplate.UnitTests.Domain.ObjectDeletes;

using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Attachments;
using FullstackTemplate.Server.Domain.ObjectDeletes;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Resources.Storage;
using Shouldly;

public class ObjectSweepTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(65);
    private static readonly string TenantId = EntityId.New(Tenant.IdPrefix);
    private static readonly HashSet<string> Tenants = [TenantId];
    private static readonly HashSet<string> NoKeys = [];

    [Test]
    public void an_object_that_no_row_names_stays_up_to_the_grace_and_goes_after_it()
    {
        // Arrange
        var atTheGrace = ObjectAged(Grace);
        var pastTheGrace = ObjectAged(Grace + TimeSpan.FromTicks(1));

        // Act
        var verdicts = new[] { atTheGrace, pastTheGrace }.Select(o => ObjectSweep.VerdictOf(o, Tenants, NoKeys, Now, Grace));

        // Assert
        verdicts.ShouldBe([SweepVerdict.Young, SweepVerdict.Orphan]);
    }

    [Test]
    public void an_object_put_after_the_start_of_the_sweep_stays()
    {
        // Arrange
        var putAfterTheStart = ObjectAged(TimeSpan.FromMinutes(-20));

        // Act
        var verdict = ObjectSweep.VerdictOf(putAfterTheStart, Tenants, NoKeys, Now, Grace);

        // Assert
        verdict.ShouldBe(SweepVerdict.Young);
    }

    [Test]
    public void an_old_object_that_a_live_row_names_stays()
    {
        // Arrange
        var named = ObjectAged(TimeSpan.FromDays(30));

        // Act
        var verdict = ObjectSweep.VerdictOf(named, Tenants, new HashSet<string> { named.Key }, Now, Grace);

        // Assert
        verdict.ShouldBe(SweepVerdict.Named);
    }

    [Test]
    public void an_old_object_of_a_tenant_that_the_database_does_not_have_stays()
    {
        // Arrange
        var ofAnotherDatabase = ObjectAged(TimeSpan.FromDays(30), EntityId.New(Tenant.IdPrefix));

        // Act
        var verdict = ObjectSweep.VerdictOf(ofAnotherDatabase, Tenants, NoKeys, Now, Grace);

        // Assert
        verdict.ShouldBe(SweepVerdict.UnknownTenant);
    }

    [Test]
    [Arguments("tenants/not-a-tenant/repairs/f1")]
    [Arguments("tenants/")]
    [Arguments("tenants//f1")]
    public void an_old_object_whose_key_names_no_tenant_stays(string key)
    {
        // Arrange
        var stored = new StoredObject(key, Now - TimeSpan.FromDays(30));

        // Act
        var verdict = ObjectSweep.VerdictOf(stored, Tenants, NoKeys, Now, Grace);

        // Assert
        verdict.ShouldBe(SweepVerdict.UnknownTenant);
    }

    [Test]
    public void the_tenant_of_a_key_is_the_tenant_id_after_the_prefix()
    {
        // Act
        var tenantOfKey = ObjectSweep.TenantOf($"{StoredFile.TenantPrefix(TenantId)}repairs/rep_1/f1");
        var tenantOfBareId = ObjectSweep.TenantOf($"{StoredFile.TenantsPrefix}{TenantId}");

        // Assert
        (tenantOfKey, tenantOfBareId).ShouldBe((TenantId, null));
    }

    [Test]
    public void a_page_gives_its_orphans_and_counts_each_object_that_stays()
    {
        // Arrange
        var orphan = ObjectAged(TimeSpan.FromDays(2));
        var named = ObjectAged(TimeSpan.FromDays(2));
        var young = ObjectAged(TimeSpan.FromMinutes(1));
        var ofAnotherDatabase = ObjectAged(TimeSpan.FromDays(2), EntityId.New(Tenant.IdPrefix));

        // Act
        var page = ObjectSweep.Decide([orphan, named, young, ofAnotherDatabase], Tenants, new HashSet<string> { named.Key }, Now, Grace);

        // Assert
        page.ShouldBe(new SweepPage([orphan.Key], Kept: 3, OfUnknownTenants: 1), new SweepPageComparer());
    }

    private static StoredObject ObjectAged(TimeSpan age, string? tenantId = null)
        => new($"{StoredFile.TenantPrefix(tenantId ?? TenantId)}repairs/rep_1/{Guid.NewGuid():N}", Now - age);

    private sealed class SweepPageComparer : IEqualityComparer<SweepPage>
    {
        public bool Equals(SweepPage? x, SweepPage? y)
            => x is not null && y is not null && x.Orphans.SequenceEqual(y.Orphans) && (x.Kept, x.OfUnknownTenants) == (y.Kept, y.OfUnknownTenants);

        public int GetHashCode(SweepPage obj) => HashCode.Combine(obj.Kept, obj.OfUnknownTenants);
    }
}
