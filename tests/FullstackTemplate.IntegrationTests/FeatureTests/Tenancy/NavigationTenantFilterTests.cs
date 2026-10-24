namespace FullstackTemplate.IntegrationTests.FeatureTests.Tenancy;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

/// <summary>
/// The test adds an entity of its own that uses <see cref="TenantingExtension.ApplyTenanting{TEntity}"/>, so it needs
/// a model and a database of its own.
/// </summary>
public sealed class NavigationTenantFilterTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    public async Task caller_of_one_tenant_sees_no_row_of_another_tenant_for_an_entity_tenanted_through_a_navigation()
    {
        // Arrange
        await _database.StartAsync();
        var tenantA = Tenant.Create(new FakeTenantForCreation().Generate());
        var tenantB = Tenant.Create(new FakeTenantForCreation().Generate());
        var userA = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, _ => tenantA.Id).Generate());
        var userB = User.Create(new FakeUserForCreation().RuleFor(u => u.TenantId, _ => tenantB.Id).Generate());
        var noteA = new Note(userA);
        var noteB = new Note(userB);

        await using (var arrange = CreateContext(TenantScope.AllTenants))
        {
            await arrange.Database.EnsureCreatedAsync();
            arrange.AddRange(tenantA, tenantB, userA, userB, noteA, noteB);
            await arrange.SaveChangesAsync();
        }

        await using var callerOfTenantA = CreateContext(TenantScope.ForTenant(tenantA.Id));

        // Act
        var noteIds = await callerOfTenantA.Set<Note>().Select(n => n.Id).ToListAsync();

        // Assert
        noteIds.ShouldBe([noteA.Id]);
    }

    private NoteContext CreateContext(TenantScope scope)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var scopeProvider = Substitute.For<ITenantScopeProvider>();
        scopeProvider.GetCurrentScope().Returns(scope);
        return new NoteContext(options, scopeProvider);
    }

    private sealed class Note : BaseEntity
    {
        public Note(User user) : this()
        {
            User = user;
            UserId = user.Id;
        }

        private Note() : base("note")
        {
        }

        public string UserId { get; private set; } = null!;
        public User User { get; private set; } = null!;
    }

    private sealed class NoteContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(), scopeProvider,
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Note>(note =>
            {
                note.HasKey(n => n.Id);
                note.Property(n => n.Id).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
                note.Property(n => n.UserId).HasMaxLength(EntityId.MaxLength).UseCollation(EntityId.Collation);
                // The user owns the note, so the audit log of the save finds the tenant of the note through the user.
                note.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
                note.ApplyTenanting(n => n.User.TenantId);
            });
            base.OnModelCreating(modelBuilder);
        }
    }
}
