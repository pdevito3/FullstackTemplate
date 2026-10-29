namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class XminMappingTests
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

    [Test]
    public void each_entity_type_of_a_tpc_hierarchy_with_an_abstract_root_reads_its_version_back_after_a_save()
    {
        // Arrange
        using var context = new AbstractTpcRootContext();

        // Act
        var versions = new[] { typeof(Note), typeof(TextNote) }
            .Select(type => context.Model.FindEntityType(type)!)
            .Select(e => e.FindProperty(nameof(BaseEntity.Version))!.ValueGenerated)
            .ToList();

        // Assert
        versions.ShouldAllBe(v => v == ValueGenerated.OnAddOrUpdate);
    }

    private abstract class Note() : BaseEntity("note");

    private sealed class TextNote : Note;

    private sealed class AbstractTpcRootContext() : AppDbContext(TestOptions,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(),
        new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.IgnoreTheAppEntities();
            modelBuilder.Entity<Note>(note =>
            {
                note.HasKey(n => n.Id);
                note.UseTpcMappingStrategy();
            });
            modelBuilder.Entity<TextNote>().HasBaseType<Note>();
        }
    }

    private static readonly DbContextOptions<AppDbContext> TestOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=unused")
        .UseSnakeCaseNamingConvention()
        .Options;

    private sealed class Shipment() : BaseEntity("shp")
    {
        public ShipmentDetail Detail { get; private set; } = null!;
    }

    private sealed class ShipmentDetail
    {
        public string ShipmentId { get; private set; } = null!;
        public string Notes { get; private set; } = null!;
    }

    private sealed class TableSplittingContext() : AppDbContext(TestOptions,
        TimeProvider.System, Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
        Substitute.For<ITenantScopeProvider>(),
        new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.IgnoreTheAppEntities();
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
