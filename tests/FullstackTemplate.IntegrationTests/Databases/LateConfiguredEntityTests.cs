namespace FullstackTemplate.IntegrationTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public sealed class LateConfiguredEntityTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task new_entity_saves_with_the_id_that_it_made()
    {
        // Arrange
        await _database.StartAsync();
        var memo = new Memo("first");

        // Act
        await AddAsync(memo);

        // Assert
        await using var reader = await CreateContextAsync();
        var savedIds = await reader.Set<Memo>().Select(m => m.Id).ToListAsync();
        savedIds.ShouldBe([memo.Id]);
    }

    [Test]
    public async Task save_of_a_row_that_changed_after_the_load_is_a_stale_version()
    {
        // Arrange
        await _database.StartAsync();
        var memo = new Memo("first");
        await AddAsync(memo);
        await using var first = await CreateContextAsync();
        await using var second = await CreateContextAsync();
        (await first.Set<Memo>().SingleAsync(m => m.Id == memo.Id)).Rewrite("from first");
        (await second.Set<Memo>().SingleAsync(m => m.Id == memo.Id)).Rewrite("from second");
        await first.SaveChangesAsync();

        // Act
        Func<Task> act = () => second.SaveChangesAsync();

        // Assert
        await act.ShouldThrowAsync<StaleVersionException>();
    }

    [Test]
    public async Task query_shows_no_deleted_row()
    {
        // Arrange
        await _database.StartAsync();
        var kept = new Memo("kept");
        var deleted = new Memo("deleted");
        await AddAsync(kept, deleted);
        await using (var deleter = await CreateContextAsync())
        {
            deleter.Remove(await deleter.Set<Memo>().SingleAsync(m => m.Id == deleted.Id));
            await deleter.SaveChangesAsync();
        }

        await using var reader = await CreateContextAsync();

        // Act
        var memoIds = await reader.Set<Memo>().Select(m => m.Id).ToListAsync();

        // Assert
        memoIds.ShouldBe([kept.Id]);
    }

    private async Task AddAsync(params Memo[] memos)
    {
        await using var writer = await CreateContextAsync();
        writer.AddRange(memos);
        await writer.SaveChangesAsync();
    }

    private async Task<MemoContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var scopeProvider = Substitute.For<ITenantScopeProvider>();
        scopeProvider.GetCurrentScope().Returns(TenantScope.AllTenants);
        var context = new MemoContext(options, scopeProvider);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private sealed class Memo : BaseEntity, ITenantable
    {
        public Memo(string text) : this() => Text = text;

        private Memo() : base("memo")
        {
        }

        [NotSensitive] public string TenantId { get; private set; } = EntityId.New(Tenant.IdPrefix);
        [NotSensitive] public string Text { get; private set; } = null!;

        public void Rewrite(string text) => Text = text;
    }

    private sealed class MemoContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), scopeProvider,
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Memo>().HasKey(m => m.Id);
        }
    }
}
