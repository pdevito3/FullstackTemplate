namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Resources.Jobs;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

internal sealed class NavigationTenantedContext() : AppDbContext(TestModelOptions.Options, TimeProvider.System,
    Substitute.For<ICurrentUserService>(), Substitute.For<ICallerIdentityResolver>(),
    Substitute.For<ITenantScopeProvider>(),
    new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()), Substitute.For<IJobWriter>())
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.IgnoreTheAppEntities();
        modelBuilder.Entity<Order>().HasKey(order => order.Id);
        modelBuilder.Entity<OrderLine>(line =>
        {
            line.HasKey(l => l.Id);
            line.ApplyTenanting(l => l.Order.TenantId);
        });
        modelBuilder.Entity<Memo>().HasKey(memo => memo.Id);
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
