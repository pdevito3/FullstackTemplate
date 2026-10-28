namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class BaseEntityShapeTests
{
    [Test]
    [Arguments("TPH")]
    [Arguments("TPT")]
    [Arguments("TPC")]
    public void a_query_of_a_derived_entity_type_has_the_soft_delete_and_tenant_filters(string mappingStrategy)
    {
        // Arrange
        using AppDbContext context = mappingStrategy switch
        {
            "TPH" => new TphContext(),
            "TPT" => new TptContext(),
            _ => new TpcContext(),
        };

        // Act
        var sql = context.Set<Cat>().ToQueryString();

        // Assert
        sql.ShouldContain("WHERE");
        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        where.ShouldContain("is_deleted");
        where.ShouldContain("tenant_id");
    }

    [Test]
    public void a_derived_entity_type_with_a_tenant_that_its_root_does_not_have_fails_the_model_with_the_fix()
    {
        // Arrange
        using var context = new DerivedTenantContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.Model);

        // Assert
        exception.Message.ShouldContain(nameof(TenantedDog));
        exception.Message.ShouldContain($"Give the tenant only to {nameof(Animal)}");
    }

    [Test]
    public void a_derived_entity_type_with_a_tenant_path_of_its_own_fails_the_model_with_the_fix()
    {
        // Arrange
        using var context = new DerivedTenantPathContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.Model);

        // Assert
        exception.Message.ShouldEndWith($"{nameof(Pet)} has a tenant, and its filter covers {nameof(Cat)}. Remove the ApplyTenanting call from {nameof(Cat)}.");
    }

    [Test]
    public void a_derived_tenantable_entity_type_under_a_root_with_a_tenant_path_fails_the_model_with_the_fix()
    {
        // Arrange
        using var context = new DerivedTenantableUnderTenantPathContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.Model);

        // Assert
        exception.Message.ShouldEndWith($"{nameof(Kennel)} has a tenant, and its filter covers {nameof(TenantedKennel)}. Remove ITenantable from {nameof(TenantedKennel)}.");
    }

    [Test]
    public void an_owned_base_entity_fails_the_model_with_the_fix()
    {
        // Arrange
        using var context = new OwnedContext();

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => context.Model);

        // Assert
        exception.Message.ShouldContain(nameof(Lid));
        exception.Message.ShouldContain("ComplexProperty");
    }

    [Test]
    public async Task save_of_an_entity_that_owns_itself_fails_with_the_fix()
    {
        // Arrange
        await using var context = new SelfOwnedContext();
        var node = new Node(EntityId.New(Tenant.IdPrefix));
        node.MoveUnder(node.Id);
        context.Add(node);

        // Act
        // The save runs on the pool, so the time limit can end a walk of the owners that never ends. The limit is on
        // the save only: Add builds the model first, and a cold build of the model can take seconds under load.
        var act = () => Task.Run(() => context.SaveChangesAsync()).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        (await act.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("form a cycle");
    }

    private abstract class ShapeContext() : AppDbContext(Options, TimeProvider.System,
        Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(), OneTenant(),
        new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        private static readonly DbContextOptions<AppDbContext> Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;

        private static ITenantScopeProvider OneTenant()
        {
            var provider = Substitute.For<ITenantScopeProvider>();
            provider.GetCurrentScope().Returns(TenantScope.ForTenant(EntityId.New(Tenant.IdPrefix)));
            return provider;
        }
    }

    private class Pet(string tenantId) : BaseEntity("pet"), ITenantable
    {
        public string TenantId { get; private set; } = tenantId;
    }

    private sealed class Cat(string tenantId) : Pet(tenantId);

    private sealed class TphContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Pet>().HasKey(p => p.Id);
            modelBuilder.Entity<Cat>().HasBaseType<Pet>();
        }
    }

    private sealed class TptContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Pet>(pet =>
            {
                pet.HasKey(p => p.Id);
                pet.UseTptMappingStrategy();
            });
            modelBuilder.Entity<Cat>().HasBaseType<Pet>();
        }
    }

    private sealed class TpcContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Pet>(pet =>
            {
                pet.HasKey(p => p.Id);
                pet.UseTpcMappingStrategy();
            });
            modelBuilder.Entity<Cat>().HasBaseType<Pet>();
        }
    }

    private sealed class DerivedTenantPathContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Pet>().HasKey(p => p.Id);
            modelBuilder.Entity<Cat>(cat =>
            {
                cat.HasBaseType<Pet>();
                cat.ApplyTenanting(c => c.TenantId);
            });
        }
    }

    private class Animal() : BaseEntity("anm");

    private sealed class TenantedDog(string tenantId) : Animal, ITenantable
    {
        public string TenantId { get; private set; } = tenantId;
    }

    private sealed class DerivedTenantContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Animal>().HasKey(a => a.Id);
            modelBuilder.Entity<TenantedDog>().HasBaseType<Animal>();
        }
    }

    private sealed class Shelter(string tenantId) : BaseEntity("shl"), ITenantable
    {
        public string TenantId { get; private set; } = tenantId;
    }

    private class Kennel() : BaseEntity("knl")
    {
        public Shelter Shelter { get; private set; } = null!;
    }

    private sealed class TenantedKennel(string tenantId) : Kennel, ITenantable
    {
        public string TenantId { get; private set; } = tenantId;
    }

    private sealed class DerivedTenantableUnderTenantPathContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Shelter>().HasKey(s => s.Id);
            modelBuilder.Entity<Kennel>(kennel =>
            {
                kennel.HasKey(k => k.Id);
                kennel.ApplyTenanting(k => k.Shelter.TenantId);
            });
            modelBuilder.Entity<TenantedKennel>().HasBaseType<Kennel>();
        }
    }

    private sealed class Node(string tenantId) : BaseEntity("nod"), ITenantable
    {
        public string TenantId { get; private set; } = tenantId;
        public string? ParentId { get; private set; }

        public void MoveUnder(string parentId) => ParentId = parentId;
    }

    private sealed class SelfOwnedContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Node>(node =>
            {
                node.HasKey(n => n.Id);
                node.HasOne<Node>().WithMany().HasForeignKey(n => n.ParentId).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }

    private sealed class Box() : BaseEntity("box")
    {
        public Lid Lid { get; private set; } = null!;
    }

    private sealed class Lid() : BaseEntity("lid");

    private sealed class OwnedContext : ShapeContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Box>(box =>
            {
                box.HasKey(b => b.Id);
                box.OwnsOne(b => b.Lid);
            });
        }
    }
}
