namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class TableSplittingTests
{
    [Test]
    public void a_type_that_shares_the_table_of_an_entity_gets_the_xmin_concurrency_token()
    {
        // Arrange
        using var context = new TableSplittingContext();

        // Act
        var detail = context.Model.FindEntityType(typeof(ShipmentDetail))!;

        // Assert
        detail.GetProperties()
            .Where(p => p.IsConcurrencyToken)
            .Select(p => p.GetColumnName())
            .ShouldBe(["xmin"]);
    }

    private sealed class Shipment() : BaseEntity("shp")
    {
        public ShipmentDetail Detail { get; private set; } = null!;
    }

    private sealed class ShipmentDetail
    {
        public string ShipmentId { get; private set; } = null!;
        public string Notes { get; private set; } = null!;
    }

    private sealed class TableSplittingContext() : AppDbContext(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=unused").UseSnakeCaseNamingConvention().Options,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(),
        new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Shipment>(shipment =>
            {
                shipment.ToTable("shipments");
                shipment.HasKey(s => s.Id);
                shipment.HasOne(s => s.Detail).WithOne().HasForeignKey<ShipmentDetail>(d => d.ShipmentId);
                shipment.Navigation(s => s.Detail).IsRequired();
            });
            modelBuilder.Entity<ShipmentDetail>(detail =>
            {
                detail.ToTable("shipments");
                detail.HasKey(d => d.ShipmentId);
                detail.Property(d => d.ShipmentId).HasColumnName("id");
            });
        }
    }
}
