namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;

/// <summary>A BaseEntity cannot map to a view, so a read model of a view is a [NotAudited] class with ITenantable.</summary>
public sealed class ReadModelViewTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task a_read_model_of_a_view_shows_only_the_rows_of_the_tenant_of_the_caller()
    {
        // Arrange
        await _database.StartAsync();
        var (mine, theirs) = (EntityId.New(Tenant.IdPrefix), EntityId.New(Tenant.IdPrefix));
        await using (var writer = await CreateContextAsync(TenantScope.AllTenants))
        {
            writer.AddRange(new Box(mine), new Box(mine), new Box(theirs));
            await writer.SaveChangesAsync();
        }

        await using (var connection = new NpgsqlConnection(_database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "CREATE VIEW box_counts AS SELECT tenant_id, count(*)::int AS count FROM boxes WHERE NOT is_deleted GROUP BY tenant_id",
                connection);
            await command.ExecuteNonQueryAsync();
        }

        // Act
        await using var reader = await CreateContextAsync(TenantScope.ForTenant(mine));
        var counts = await reader.Set<BoxCount>().ToListAsync();

        // Assert
        counts.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            c => c.TenantId.ShouldBe(mine),
            c => c.Count.ShouldBe(2));
    }

    private async Task<AppDbContext> CreateContextAsync(TenantScope scope)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var scopeProvider = Substitute.For<ITenantScopeProvider>();
        scopeProvider.GetCurrentScope().Returns(scope);
        var context = new ReadModelContext(options, scopeProvider);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class Box(string tenantId) : BaseEntity("box"), ITenantable
    {
        [NotSensitive] public string TenantId { get; private set; } = tenantId;
    }

    [NotAudited("A read model of a view. The save never writes it.")]
    private sealed class BoxCount : ITenantable
    {
        public string TenantId { get; private set; } = null!;
        public int Count { get; private set; }
    }

    private sealed class ReadModelContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), scopeProvider,
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Box>().ToTable("boxes").HasKey(b => b.Id);
            modelBuilder.Entity<BoxCount>().HasNoKey().ToView("box_counts");
        }
    }
}
