namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class TenantOwnedRowsTests
{
    [Test]
    public void a_tenant_delete_loads_each_owner_before_its_children_whatever_their_names()
    {
        // Arrange
        using var context = new ShippingContext();

        // Act
        var loadOrder = TenantOwnedRows.InLoadOrder(context.Model).Select(e => e.ClrType).ToList();

        // Assert
        loadOrder.ShouldContain(typeof(Label));
        loadOrder.IndexOf(typeof(Shipment)).ShouldBeLessThan(loadOrder.IndexOf(typeof(Parcel)));
        loadOrder.IndexOf(typeof(Parcel)).ShouldBeLessThan(loadOrder.IndexOf(typeof(Label)));
    }

    private sealed class Shipment() : BaseEntity("shp"), ITenantable
    {
        public string TenantId { get; private set; } = null!;
    }

    private sealed class Parcel() : BaseEntity("prc")
    {
        public Shipment Shipment { get; private set; } = null!;
    }

    private sealed class Label() : BaseEntity("lbl")
    {
        public Parcel Parcel { get; private set; } = null!;
    }

    private sealed class ShippingContext() : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(), new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Shipment>().HasKey(s => s.Id);
            modelBuilder.Entity<Parcel>(parcel =>
            {
                parcel.HasKey(p => p.Id);
                parcel.HasOne(p => p.Shipment).WithMany().OnDelete(DeleteBehavior.Cascade);
                parcel.ApplyTenanting(p => p.Shipment.TenantId);
            });
            modelBuilder.Entity<Label>(label =>
            {
                label.HasKey(l => l.Id);
                label.HasOne(l => l.Parcel).WithMany().OnDelete(DeleteBehavior.Cascade);
                label.ApplyTenanting(l => l.Parcel.Shipment.TenantId);
            });
        }
    }
}
