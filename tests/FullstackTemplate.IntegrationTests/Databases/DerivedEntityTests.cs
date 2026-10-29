namespace FullstackTemplate.IntegrationTests.Databases;

using System.Text.Json;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.AuditEntries;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

public sealed class DerivedEntityTests : IAsyncDisposable
{
    private readonly PostgresTestDatabase _database = new();

    public async ValueTask DisposeAsync() => await _database.DisposeAsync();

    [Test]
    [Arguments("TPH")]
    [Arguments("TPT")]
    [Arguments("TPC")]
    public async Task derived_entity_saves_updates_and_soft_deletes_with_an_audit_entry_for_each_change(
        string mappingStrategy)
    {
        // Arrange
        await _database.StartAsync();
        var cat = new Cat("Tom", "grey");

        // Act
        // The saves run on the pool, so the time limit can end a walk of the owners that never ends.
        await Task.Run(() => AddUpdateAndDeleteAsync(mappingStrategy, cat)).WaitAsync(TimeSpan.FromSeconds(30));

        // Assert
        await using var reader = await CreateContextAsync(mappingStrategy);
        (await reader.Set<Cat>().AnyAsync(c => c.Id == cat.Id)).ShouldBeFalse();
        (await reader.Set<Cat>().IgnoreQueryFilters().SingleAsync(c => c.Id == cat.Id))
            .ShouldSatisfyAllConditions(c => c.Name.ShouldBe("Thomas"), c => c.Tag.ShouldBe("black"));
        var entries = await reader.AuditEntries.Where(e => e.RootId == cat.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Ordinal).ToListAsync();
        entries.Select(e => e.Kind).ShouldBe([AuditEntryKind.Created, AuditEntryKind.Updated, AuditEntryKind.Deleted]);
        entries.ShouldAllBe(e => e.EntityType == nameof(Cat) && e.EntityId == cat.Id && e.RootType == nameof(Cat)
                                 && e.TenantId == cat.TenantId);
        Paths(entries[0]).ShouldBe([nameof(Pet.TenantId), nameof(Pet.Name), nameof(Cat.Tag)], ignoreOrder: true);
        Paths(entries[1]).ShouldBe([nameof(Pet.Name), nameof(Cat.Tag)], ignoreOrder: true);
    }

    [Test]
    [Arguments("TPH")]
    [Arguments("TPC")]
    public async Task bulk_insert_of_a_derived_entity_writes_its_row_and_a_created_entry(string mappingStrategy)
    {
        // Arrange
        await _database.StartAsync();
        var cat = new Cat("Tom", "grey");

        // Act
        await using (var writer = await CreateContextAsync(mappingStrategy))
            await writer.BulkInsertAsync([cat]);

        // Assert
        await using var reader = await CreateContextAsync(mappingStrategy);
        (await reader.Set<Cat>().SingleAsync(c => c.Id == cat.Id)).Tag.ShouldBe("grey");
        var created = await reader.AuditEntries.SingleAsync(e => e.RootId == cat.Id);
        (created.Kind, created.EntityType).ShouldBe((AuditEntryKind.Created, nameof(Cat)));
        Paths(created).ShouldBe([nameof(Pet.TenantId), nameof(Pet.Name), nameof(Cat.Tag)], ignoreOrder: true);
    }

    [Test]
    public async Task bulk_insert_of_a_tpt_derived_entity_is_refused()
    {
        // Arrange
        await _database.StartAsync();
        await using var writer = await CreateContextAsync("TPT");

        // Act
        var act = () => writer.BulkInsertAsync([new Cat("Tom", "grey")]);

        // Assert
        (await act.ShouldThrowAsync<NotSupportedException>()).Message.ShouldContain("more than one table");
    }

    private async Task AddUpdateAndDeleteAsync(string mappingStrategy, Cat cat)
    {
        await using (var writer = await CreateContextAsync(mappingStrategy))
        {
            writer.Add(cat);
            await writer.SaveChangesAsync();
        }

        await using (var updater = await CreateContextAsync(mappingStrategy))
        {
            var loaded = await updater.Set<Cat>().SingleAsync(c => c.Id == cat.Id);
            loaded.Retag("Thomas", "black", loaded.Version);
            await updater.SaveChangesAsync();
        }

        await using var deleter = await CreateContextAsync(mappingStrategy);
        deleter.Remove(await deleter.Set<Cat>().SingleAsync(c => c.Id == cat.Id));
        await deleter.SaveChangesAsync();
    }

    private static List<string> Paths(AuditEntry entry)
        => JsonDocument.Parse(entry.Data).RootElement.EnumerateObject().Select(p => p.Name).ToList();

    private async Task<AppDbContext> CreateContextAsync(string mappingStrategy)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        var scopeProvider = Substitute.For<ITenantScopeProvider>();
        scopeProvider.GetCurrentScope().Returns(TenantScope.AllTenants);
        AppDbContext context = mappingStrategy switch
        {
            "TPH" => new TphContext(options, scopeProvider),
            "TPT" => new TptContext(options, scopeProvider),
            _ => new TpcContext(options, scopeProvider),
        };
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private abstract class Pet : BaseEntity, ITenantable
    {
        protected Pet(string name) : this() => Name = name;

        protected Pet() : base("pet")
        {
        }

        [NotSensitive] public string TenantId { get; private set; } = EntityId.New(Tenant.IdPrefix);
        [NotSensitive] public string Name { get; protected set; } = null!;
    }

    private sealed class Cat : Pet
    {
        public Cat(string name, string tag) : base(name) => Tag = tag;

        private Cat()
        {
        }

        [NotSensitive] public string Tag { get; private set; } = null!;

        public void Retag(string name, string tag, uint version)
        {
            GuardVersion(version);
            Name = name;
            Tag = tag;
        }
    }

    private abstract class PetContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : AppDbContext(options, TimeProvider.System, Substitute.For<ICurrentUserService>(),
            Substitute.For<ICallerIdentityResolver>(), scopeProvider,
            new DomainEventDispatcher(new ServiceCollection().BuildServiceProvider()))
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.IgnoreTheAppEntities();
            var pet = modelBuilder.Entity<Pet>();
            pet.HasKey(p => p.Id);
            MapPets(pet);
            modelBuilder.Entity<Cat>().HasBaseType<Pet>();
        }

        protected abstract void MapPets(EntityTypeBuilder<Pet> pet);
    }

    private sealed class TphContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : PetContext(options, scopeProvider)
    {
        protected override void MapPets(EntityTypeBuilder<Pet> pet)
            => pet.UseTphMappingStrategy();
    }

    private sealed class TptContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : PetContext(options, scopeProvider)
    {
        protected override void MapPets(EntityTypeBuilder<Pet> pet)
            => pet.UseTptMappingStrategy();
    }

    private sealed class TpcContext(DbContextOptions<AppDbContext> options, ITenantScopeProvider scopeProvider)
        : PetContext(options, scopeProvider)
    {
        protected override void MapPets(EntityTypeBuilder<Pet> pet)
            => pet.UseTpcMappingStrategy();
    }
}
