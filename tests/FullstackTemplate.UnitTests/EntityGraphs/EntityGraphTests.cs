namespace FullstackTemplate.UnitTests.EntityGraphs;

using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using Shouldly;

public class EntityGraphTests
{
    [Test]
    public void customizer_sets_only_the_given_values()
    {
        // Act
        TestGraph.Start(out _)
            .AddUser(out var user, u => u.FirstName = "Ada");

        // Assert
        user.FirstName.ShouldBe("Ada");
        user.LastName.ShouldNotBeNullOrWhiteSpace();
        user.Identifier.ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    public void derive_does_not_overwrite_an_explicit_value()
    {
        // Act - an empty string is explicit, so the derive keeps it and the domain rejects it
        var act = () => TestGraph.Start(out _)
            .AddUser(out _, u => u.FirstName = "");

        // Assert
        Should.Throw<ValidationException>(act);
    }

    [Test]
    public void each_customizer_makes_a_new_entity()
    {
        // Act
        TestGraph.Start(out _)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User");

        // Assert
        users.Length.ShouldBe(2);
        users[0].Role.ShouldBe(UserRole.Admin());
        users[1].Role.ShouldBe(UserRole.User());
    }

    [Test]
    public void stacked_customizers_apply_to_one_entity()
    {
        // Arrange
        Action<UserInput> admin = u => u.Role = "Admin";
        Action<UserInput> named = u => u.FirstName = "Grace";

        // Act
        TestGraph.Start(out _)
            .AddUsers(out var users, admin + named);

        // Assert
        users.ShouldHaveSingleItem();
        users[0].FirstName.ShouldBe("Grace");
        users[0].Role.ShouldBe(UserRole.Admin());
    }

    [Test]
    public void batch_entities_are_distinct()
    {
        // Act
        TestGraph.Start(out _)
            .AddUsers(out var users, 3);

        // Assert
        users.Select(u => u.Id).Distinct().Count().ShouldBe(3);
        users.Select(u => u.Identifier).Distinct().Count().ShouldBe(3);
    }

    [Test]
    public void blueprints_give_each_entity_an_id_with_its_prefix()
    {
        // Act
        TestGraph.Start(out _)
            .AddTenant(out var tenant)
            .AddUser(out var user);

        // Assert
        tenant.Id.ShouldStartWith($"{Tenant.IdPrefix}_");
        user.Id.ShouldStartWith($"{User.IdPrefix}_");
        user.UserPermissions.ShouldAllBe(p => p.UserId == user.Id);
    }

    [Test]
    public void customizer_can_set_the_id()
    {
        // Act
        TestGraph.Start(out _)
            .AddTenant(out var tenant, t => t.Id = "ten_fixed")
            .AddUser(out var user, u => { u.Id = "usr_fixed"; u.Role = "Admin"; });

        // Assert
        tenant.Id.ShouldBe("ten_fixed");
        user.Id.ShouldBe("usr_fixed");
        user.TenantId.ShouldBe("ten_fixed");
        user.UserPermissions.ShouldNotBeEmpty();
        user.UserPermissions.ShouldAllBe(p => p.UserId == "usr_fixed");
    }

    [Test]
    public void customizer_id_must_have_the_entity_prefix()
    {
        // Act
        var act = () => TestGraph.Start(out _)
            .AddUser(out _, u => u.Id = "ten_wrong");

        // Assert
        Should.Throw<ValidationException>(act);
    }

    [Test]
    public void child_gets_tenant_from_the_seed_anchor()
    {
        // Act
        TestGraph.Start(out var seed)
            .AddUser(out var user);

        // Assert
        user.TenantId.ShouldBe(seed.Tenant.Id);
    }

    [Test]
    public void child_gets_tenant_from_a_new_parent()
    {
        // Act
        TestGraph.Start(out var seed)
            .AddTenant(out var tenant)
            .AddUser(out var user);

        // Assert
        user.TenantId.ShouldBe(tenant.Id);
        user.TenantId.ShouldNotBe(seed.Tenant.Id);
    }

    [Test]
    public void agent_gets_tenant_from_its_parent()
    {
        // Act
        TestGraph.Start(out var seed)
            .AddTenant(out var tenant)
            .AddAgent(out var agent, a => a.Permissions = [Permission.Names.ManageTenant]);

        // Assert
        agent.TenantId.ShouldBe(tenant.Id);
        agent.TenantId.ShouldNotBe(seed.Tenant.Id);
        agent.Permissions.ShouldHaveSingleItem().AgentId.ShouldBe(agent.Id);
    }

    [Test]
    public void children_fan_out_per_parent()
    {
        // Act
        TestGraph.Start(out _)
            .AddTenants(out var tenants, 2)
            .AddUsers(out var users, 2);

        // Assert
        users.Length.ShouldBe(4);
        users.Count(u => u.TenantId == tenants[0].Id).ShouldBe(2);
        users.Count(u => u.TenantId == tenants[1].Id).ShouldBe(2);
    }

    [Test]
    public void under_re_anchors_to_an_owned_parent()
    {
        // Act
        var graph = TestGraph.Start(out var seed)
            .AddTenant(out var other)
            .AddUser(out var otherUser);
        graph.Under(seed.Tenant)
            .AddUser(out var seedUser);

        // Assert
        otherUser.TenantId.ShouldBe(other.Id);
        seedUser.TenantId.ShouldBe(seed.Tenant.Id);
    }

    [Test]
    public void under_rejects_a_parent_that_the_session_does_not_own()
    {
        // Arrange
        var stranger = TestSeed.Generate().Tenant;

        // Act
        var act = () => TestGraph.Start(out _).Under(stranger);

        // Assert
        Should.Throw<ArgumentException>(act);
    }

    [Test]
    public async Task build_saves_created_entities_once_and_not_the_seed()
    {
        // Arrange
        var store = new RecordingStore();
        var seed = TestSeed.Generate();

        // Act
        await TestGraph.Start(seed, store)
            .AddTenant(out var tenant)
            .AddUsers(out var users, 2)
            .BuildAsync();

        // Assert
        store.Saves.ShouldHaveSingleItem();
        store.Saves[0].ShouldBe([tenant, users[0], users[1]]);
        store.Saves[0].ShouldNotContain(seed.Tenant);
        store.Saves[0].ShouldNotContain(seed.User);
    }

    [Test]
    public async Task build_runs_only_once()
    {
        // Arrange
        var graph = TestGraph.Start(out _);
        await graph.AddUser(out _).BuildAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(() => graph.BuildAsync());
        Should.Throw<InvalidOperationException>(() => graph.AddUser(out _));
    }

    [Test]
    public void a_failed_add_faults_the_session()
    {
        // Arrange
        var graph = TestGraph.Start(out _);
        Should.Throw<ValidationException>(() => graph.AddUser(out _, u => u.FirstName = ""));

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => graph.AddUser(out _));

        // Assert
        exception.InnerException.ShouldBeOfType<ValidationException>();
    }

    private sealed class RecordingStore : IGraphStore
    {
        public List<IReadOnlyList<object>> Saves { get; } = [];

        public ValueTask SaveAsync(IReadOnlyList<object> entities, CancellationToken cancellationToken)
        {
            Saves.Add(entities);
            return ValueTask.CompletedTask;
        }
    }
}
