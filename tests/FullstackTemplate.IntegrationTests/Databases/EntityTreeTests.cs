namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public sealed class EntityTreeTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task top_node_with_no_parent_is_its_own_root_and_a_child_node_saves_under_it()
    {
        // Arrange
        await _database.StartAsync();
        var top = Node.Top();

        // Act
        await using (var writer = await CreateContextAsync())
        {
            writer.Add(top);
            await writer.SaveChangesAsync();
        }

        Node child;
        await using (var writer = await CreateContextAsync())
        {
            var loaded = await writer.Set<Node>().SingleAsync(n => n.Id == top.Id);
            child = loaded.NewChild();
            writer.Add(child);
            await writer.SaveChangesAsync();
        }

        // Assert
        await using var reader = await CreateContextAsync();
        (await reader.Set<Node>().SingleAsync(n => n.Id == child.Id)).ParentId.ShouldBe(top.Id);
        var topCreated = await reader.AuditEntries.SingleAsync(e => e.EntityId == top.Id);
        topCreated.ShouldSatisfyAllConditions(
            e => e.Kind.ShouldBe(AuditEntryKind.Created),
            e => (e.RootType, e.RootId).ShouldBe((nameof(Node), top.Id)),
            e => e.TenantId.ShouldBe(top.TenantId));
        var childCreated = await reader.AuditEntries.SingleAsync(e => e.EntityId == child.Id);
        childCreated.ShouldSatisfyAllConditions(
            e => e.Kind.ShouldBe(AuditEntryKind.Created),
            e => e.EntityType.ShouldBe(nameof(Node)));
    }

    private async Task<AppDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var scopeProvider = Substitute.For<ITenantScopeProvider>();
        scopeProvider.GetCurrentScope().Returns(TenantScope.AllTenants);
        var context = new TreeContext(options, scopeProvider);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class Node : BaseEntity, ITenantable
    {
        private Node() : base("node")
        {
        }

        public static Node Top() => new();

        public Node NewChild() => new() { TenantId = TenantId, ParentId = Id };

        [NotSensitive] public string TenantId { get; private set; } = EntityId.New(Tenant.IdPrefix);
        public string? ParentId { get; private set; }
        public Node? Parent { get; private set; }
    }

    private sealed class TreeContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), scopeProvider,
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var node = modelBuilder.Entity<Node>();
            node.HasKey(n => n.Id);
            node.HasOne(n => n.Parent).WithMany().HasForeignKey(n => n.ParentId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
