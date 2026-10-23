namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

/// <summary>
/// The model of <see cref="AppDbContext"/> plus an <see cref="OrderLine"/> that gets its tenant through its order,
/// and a <see cref="Memo"/> that has no tenant.
/// </summary>
internal sealed class NavigationTenantedContext() : AppDbContext(TestModelOptions.Options, TimeProvider.System,
    Substitute.For<ICurrentUserService>(), Substitute.For<ITenantScopeProvider>(),
    new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasKey(order => order.Id);
        modelBuilder.Entity<OrderLine>(line =>
        {
            line.HasKey(l => l.Id);
            line.ApplyTenanting(l => l.Order.TenantId);
        });
        modelBuilder.Entity<Memo>().HasKey(memo => memo.Id);
        base.OnModelCreating(modelBuilder);
    }
}

/// <summary>The model of <see cref="AppDbContext"/>, then an <see cref="OrderLine"/> that calls ApplyTenanting after it.</summary>
internal sealed class LateNavigationTenantedContext() : AppDbContext(TestModelOptions.Options, TimeProvider.System,
    Substitute.For<ICurrentUserService>(), Substitute.For<ITenantScopeProvider>(),
    new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Order>().HasKey(order => order.Id);
        modelBuilder.Entity<OrderLine>(line =>
        {
            line.HasKey(l => l.Id);
            line.ApplyTenanting(l => l.Order.TenantId);
        });
    }
}

file static class TestModelOptions
{
    public static readonly DbContextOptions<AppDbContext> Options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=unused")
        .UseSnakeCaseNamingConvention()
        .Options;
}

internal sealed class Order(string tenantId) : BaseEntity("ord"), ITenantable
{
    public string TenantId { get; private set; } = tenantId;
}

internal sealed class OrderLine : BaseEntity
{
    public OrderLine(Order order) : this() => Order = order;

    private OrderLine() : base("oln")
    {
    }

    public Order Order { get; private set; } = null!;
}

internal sealed class Memo() : BaseEntity("memo");
