namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public class TenantOwnedRowsTests
{
    private static readonly Dictionary<Type, string> TypesWhoseRowsOutliveTheirTenant = new()
    {
        [typeof(AuditEntry)] = "The history of a deleted tenant stays readable.",
    };

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

    [Test]
    public void each_entity_type_with_a_tenant_id_has_the_tenant_filter_and_the_tenant_delete_deletes_each_tenant_table()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var missed = TenantIdTypesThatTheTenancyMisses(context.Model);

        // Assert
        missed.ShouldBeEmpty(
            "A type with a TenantId holds the data of a tenant, so it always needs the tenant filter. Implement ITenantable on it, or call ApplyTenanting in its configuration. The tenant delete deletes only BaseEntity rows, so make a type whose rows go with their tenant a BaseEntity. If the rows of a type must stay after the delete of their tenant, add it to the list of this test with the reason.");
    }

    [Test]
    public void each_type_whose_rows_outlive_their_tenant_gives_a_reason()
    {
        // Act
        var noReason = TypesWhoseRowsOutliveTheirTenant
            .Where(type => string.IsNullOrWhiteSpace(type.Value))
            .Select(type => type.Key.Name);

        // Assert
        noReason.ShouldBeEmpty("Give each type in the list the reason that its rows stay after the delete of their tenant.");
    }

    [Test]
    public void the_tenant_check_finds_a_type_with_no_tenant_filter_and_each_table_that_the_tenant_delete_skips()
    {
        // Arrange
        using var context = new ShippingContext();

        // Act
        var missed = TenantIdTypesThatTheTenancyMisses(context.Model);

        // Assert
        missed.ShouldBe(
            [
                $"{nameof(Invoice)}: no tenant filter", $"{nameof(Invoice)}: not deleted",
                $"{nameof(ShipmentReport)}: not deleted", $"{nameof(ShipmentNote)}: not deleted",
            ],
            ignoreOrder: true);
    }

    private static List<string> TenantIdTypesThatTheTenancyMisses(IModel model)
    {
        var deleted = TenantOwnedRows.InLoadOrder(model).Append(model.FindEntityType(typeof(Tenant))!).ToHashSet();
        var roots = model.GetEntityTypes().Where(e => e.BaseType is null).ToList();
        return
        [
            .. roots.Where(root => HasTenantId(root) && !HasTenantFilter(root))
                .Select(root => $"{root.ClrType.Name}: no tenant filter"),
            .. roots.Where(root => (HasTenantId(root) || HasTenantFilter(root))
                                   && HoldsRowsOfItsOwn(root)
                                   && !deleted.Contains(root)
                                   && !TypesWhoseRowsOutliveTheirTenant.ContainsKey(root.ClrType))
                .Select(root => $"{root.ClrType.Name}: not deleted"),
        ];
    }

    private static bool HasTenantId(IEntityType root)
        => root.GetDerivedTypesInclusive().Any(e => e.FindProperty(nameof(ITenantable.TenantId)) is not null);

    private static bool HasTenantFilter(IEntityType root)
        => root.FindDeclaredQueryFilter(QueryFilterNames.Tenant) is not null;

    private static bool HoldsRowsOfItsOwn(IEntityType root)
        => root.GetDerivedTypesInclusive().Any(e => e.GetTableName() is not null);

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

    private sealed class Invoice() : BaseEntity("inv")
    {
        public string TenantId { get; private set; } = null!;
        public Shipment Shipment { get; private set; } = null!;
    }

    [NotAudited("A test read model.")]
    private sealed class ShipmentReport : ITenantable
    {
        public string Id { get; private set; } = null!;
        public string TenantId { get; private set; } = null!;
    }

    [NotAudited("A test read model.")]
    private sealed class ShipmentNote
    {
        public string Id { get; private set; } = null!;
        public Shipment Shipment { get; private set; } = null!;
    }

    [NotAudited("A test view.")]
    private sealed class ShipmentSummary : ITenantable
    {
        public string TenantId { get; private set; } = null!;
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
            modelBuilder.Entity<Invoice>(invoice =>
            {
                invoice.HasKey(i => i.Id);
                invoice.HasOne(i => i.Shipment).WithMany().OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<ShipmentReport>().HasKey(r => r.Id);
            modelBuilder.Entity<ShipmentNote>(note =>
            {
                note.HasKey(n => n.Id);
                note.ApplyTenanting(n => n.Shipment.TenantId);
            });
            modelBuilder.Entity<ShipmentSummary>().HasNoKey().ToView("shipment_summaries");
        }
    }
}
