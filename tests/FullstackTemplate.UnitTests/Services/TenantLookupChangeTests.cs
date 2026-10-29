namespace FullstackTemplate.UnitTests.Services;

using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using static FullstackTemplate.Server.Services.TenantLookupChange;

public class TenantLookupChangeTests
{
    private const string TenantA = "ten_a";
    private const string TenantB = "ten_b";

    public static IEnumerable<(string Scenario, UserEntry Entry, string[] Stale)> UserCases =>
    [
        ("added user", Entry(EntityState.Added, new("new", TenantA, false)), ["new"]),
        ("unchanged user", Entry(EntityState.Unchanged, new("same", TenantA, false)), []),
        ("modified user with the same lookup values", Entry(EntityState.Modified, new("same", TenantA, false)), []),
        ("changed identifier", Entry(EntityState.Modified, new("old", TenantA, false), new("new", TenantA, false)), ["old", "new"]),
        ("moved to another tenant", Entry(EntityState.Modified, new("moved", TenantA, false), new("moved", TenantB, false)), ["moved"]),
        ("soft delete", Entry(EntityState.Modified, new("gone", TenantA, false), new("gone", TenantA, true)), ["gone"]),
        ("restore", Entry(EntityState.Modified, new("back", TenantA, true), new("back", TenantA, false)), ["back"]),
    ];

    public static IEnumerable<(string Scenario, TenantEntry Entry, string[] Changed)> TenantCases =>
    [
        ("same deletion state", new(TenantA, false, false), []),
        ("soft delete", new(TenantA, false, true), [TenantA]),
        ("restore", new(TenantA, true, false), [TenantA]),
    ];

    [Test]
    [MethodDataSource(nameof(UserCases))]
    public void user_change_decides_the_stale_identifiers(string scenario, UserEntry entry, string[] stale)
    {
        // Act
        var change = Read([entry], []);

        // Assert
        change.StaleIdentifiers.ShouldBe(stale, ignoreOrder: true, customMessage: scenario);
        change.ChangedTenantIds.ShouldBeEmpty(scenario);
    }

    [Test]
    [MethodDataSource(nameof(TenantCases))]
    public void tenant_change_decides_the_changed_tenants(string scenario, TenantEntry entry, string[] changed)
    {
        // Act
        var change = Read([], [entry]);

        // Assert
        change.ChangedTenantIds.ShouldBe(changed, ignoreOrder: true, customMessage: scenario);
        change.StaleIdentifiers.ShouldBeEmpty(scenario);
    }

    [Test]
    public void one_save_gives_each_stale_identifier_once()
    {
        // Act
        var change = Read(
            [
                Entry(EntityState.Modified, new("old", TenantA, false), new("new", TenantA, false)),
                Entry(EntityState.Added, new("new", TenantA, false)),
            ],
            []);

        // Assert
        change.StaleIdentifiers.ShouldBe(["old", "new"], ignoreOrder: true);
    }

    private static UserEntry Entry(EntityState state, UserLookup original, UserLookup? current = null)
        => new(state, original, current ?? original);
}
