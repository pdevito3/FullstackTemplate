namespace FullstackTemplate.UnitTests.Databases.Auditing;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

/// <summary>The rules that the model of <see cref="AppDbContext"/> must keep, so the save can audit each change.</summary>
public class AuditModelTests
{
    [Test]
    public void each_aggregate_root_has_a_tenant_of_its_own()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var rootsWithNoTenant = RootsWithNoTenant(context.Model);

        // Assert
        rootsWithNoTenant.ShouldBeEmpty(
            "The audit log reads the tenant of a change from its aggregate root, and an entity with an optional owner key is a root when the key is null. Implement ITenantable on each entity that can be a root, or give it a required Cascade foreign key to its owner.");
    }

    [Test]
    public void each_entity_with_a_tenant_of_its_own_is_an_aggregate_root()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var tenantedChildren = TenantedChildren(context.Model);

        // Assert
        tenantedChildren.ShouldBeEmpty(
            "AuditSubject.Of files an action under the entity that it names as the aggregate root. Remove ITenantable from a child with a required owner key, because its owner gives its tenant.");
    }

    [Test]
    public void a_tree_whose_nodes_have_a_tenant_keeps_the_tenant_rules()
    {
        // Arrange
        using var context = new TreeContext<TenantedNode>();

        // Act
        var rootsWithNoTenant = RootsWithNoTenant(context.Model);
        var tenantedChildren = TenantedChildren(context.Model);

        // Assert
        rootsWithNoTenant.ShouldBeEmpty();
        tenantedChildren.ShouldBeEmpty();
    }

    [Test]
    public void a_tree_whose_nodes_have_no_tenant_breaks_the_tenant_rule_of_a_root()
    {
        // Arrange
        using var context = new TreeContext<UntenantedNode>();

        // Act
        var rootsWithNoTenant = RootsWithNoTenant(context.Model);

        // Assert
        rootsWithNoTenant.ShouldBe([typeof(UntenantedNode)]);
    }

    [Test]
    public void an_entity_whose_key_to_its_owner_is_not_cascade_is_a_root_that_needs_a_tenant_of_its_own()
    {
        // Arrange
        using var context = new RestrictedChildContext();

        // Act
        var rootsWithNoTenant = RootsWithNoTenant(context.Model);

        // Assert
        rootsWithNoTenant.ShouldBe([typeof(RestrictedChild)]);
    }

    [Test]
    public void no_entity_type_is_owned()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var owned = context.Model.GetEntityTypes().Where(e => e.IsOwned()).Select(e => e.ClrType).ToList();

        // Assert
        owned.ShouldBeEmpty(
            "The audit log reads only entities and their complex properties. Map a value object with ComplexProperty, not OwnsOne.");
    }

    [Test]
    public void each_entity_type_has_a_name_of_its_own()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var sharedNames = EntityTypes(context.Model)
            .GroupBy(e => e.ClrType.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        // Assert
        sharedNames.ShouldBeEmpty("An audit entry names its entity type by the class name, so two entity classes must not share a name.");
    }

    [Test]
    public void each_entity_type_that_the_save_does_not_audit_says_why()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unexplained = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(type => !type.IsAssignableTo(typeof(BaseEntity)) && type != typeof(AuditEntry))
            .Where(type => string.IsNullOrWhiteSpace(
                ((NotAuditedAttribute?)Attribute.GetCustomAttribute(type, typeof(NotAuditedAttribute)))?.Reason))
            .ToList();

        // Assert
        unexplained.ShouldBeEmpty(
            "The save audits only a BaseEntity. Make the type a BaseEntity, or give it [NotAudited(reason)] with the reason that its rows need no audit.");
    }

    [Test]
    public void no_audited_entity_type_has_the_not_audited_mark()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var marked = context.Model.GetEntityTypes()
            .Select(e => e.ClrType)
            .Where(type => (type.IsAssignableTo(typeof(BaseEntity)) || type == typeof(AuditEntry))
                           && Attribute.IsDefined(type, typeof(NotAuditedAttribute)))
            .ToList();

        // Assert
        marked.ShouldBeEmpty(
            "The save audits each BaseEntity, so the write guard must keep its table. Remove [NotAudited] from it.");
    }

    [Test]
    public void guarded_tables_are_the_audited_tables_and_the_audit_log()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var guarded = AuditModel.Of(context.Model).GuardedTables;

        // Assert
        guarded.ShouldContain("users");
        guarded.ShouldContain("audit_entries");
        guarded.ShouldNotContain("used_dpop_proofs");
    }

    [Test]
    public void no_column_that_a_write_sends_gets_its_value_from_the_database_on_insert()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var generated = EntityTypes(context.Model)
            .SelectMany(EntityColumn.Of)
            .Where(column => !column.Property.IsPrimaryKey() && column.Property.ValueGenerated == ValueGenerated.OnAdd)
            .Select(column => $"{column.Property.DeclaringType.DisplayName()}.{column.Property.Name}")
            .ToList();

        // Assert
        generated.ShouldBeEmpty(
            "The COPY of BulkInsertAsync sends the C# value of each column, and the audit entry records it, so a database default or an identity never applies. Set the value in the entity, and remove HasDefaultValueSql, HasDefaultValue, or the identity.");
    }

    // An entity with an optional owner key is a root when the key is null, so it needs a tenant of its own.
    private static List<Type> RootsWithNoTenant(IModel model)
    {
        var auditModel = AuditModel.Of(model);
        return EntityTypes(model)
            .Where(e => auditModel.OwnerKeyOf(e) is not { IsRequired: true })
            .Select(e => e.ClrType)
            .Where(type => type != typeof(Tenant) && !type.IsAssignableTo(typeof(ITenantable)))
            .ToList();
    }

    private static List<Type> TenantedChildren(IModel model)
    {
        var auditModel = AuditModel.Of(model);
        return EntityTypes(model)
            .Where(e => e.ClrType.IsAssignableTo(typeof(ITenantable)) && auditModel.OwnerKeyOf(e) is { IsRequired: true })
            .Select(e => e.ClrType)
            .ToList();
    }

    private static IEnumerable<IEntityType> EntityTypes(IModel model)
        => model.GetEntityTypes().Where(e => e.ClrType.IsAssignableTo(typeof(BaseEntity)));

    private sealed class TenantedNode() : BaseEntity("tnd"), ITenantable
    {
        public string TenantId { get; private set; } = EntityId.New(Tenant.IdPrefix);
        public string? ParentId { get; private set; }
    }

    private sealed class UntenantedNode() : BaseEntity("und")
    {
        public string? ParentId { get; private set; }
    }

    private sealed class TenantedOwner() : BaseEntity("tow"), ITenantable
    {
        public string TenantId { get; private set; } = EntityId.New(Tenant.IdPrefix);
    }

    private sealed class RestrictedChild() : BaseEntity("rch")
    {
        public TenantedOwner Owner { get; private set; } = null!;
    }

    private sealed class RestrictedChildContext() : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TenantedOwner>().HasKey(o => o.Id);
            modelBuilder.Entity<RestrictedChild>(child =>
            {
                child.HasKey(c => c.Id);
                child.HasOne(c => c.Owner).WithMany().IsRequired().OnDelete(DeleteBehavior.Restrict);
                child.ApplyTenanting(c => c.Owner.TenantId);
            });
        }
    }

    private sealed class TreeContext<TNode>() : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
        where TNode : BaseEntity
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TNode>(node =>
            {
                node.HasKey(n => n.Id);
                node.HasOne<TNode>().WithMany().HasForeignKey("ParentId").OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
