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
        exception.Message.ShouldContain(nameof(Cat));
        exception.Message.ShouldContain($"Give the tenant only to {nameof(Pet)}");
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
