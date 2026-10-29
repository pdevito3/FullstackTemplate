namespace FullstackTemplate.UnitTests.Architecture;

using System.Reflection;
using System.Runtime.CompilerServices;
using FullstackTemplate.ArchitectureFixtures;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Databases.Auditing;
using FullstackTemplate.Server.Domain.Tenants;
using Microsoft.EntityFrameworkCore.Metadata;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shouldly;

/// <summary>
/// <c>DeleteTenant</c> takes the exclusive <see cref="RootLock.Tenant"/> and deletes each row of the tenant. So each add
/// of a tenant-owned aggregate root adds and saves in the work of the shared lock of its tenant, or a row that is saved
/// during the delete stays live in the deleted tenant. The rule reads the IL with Mono.Cecil. It finds each call that
/// adds a root of the EF model to a <c>DbSet</c> or to the context, and checks that the call is in a lambda that the
/// same method gives to <see cref="RootLock.RunSharedAsync{T}"/>. A type that also adds a <see cref="Tenant"/> adds to
/// a new tenant, which no delete can see yet. A child has a required owner, so its add changes the version of the owner
/// instead. See <c>docs/backend/concurrency.md</c>, section "Lock".
/// </summary>
public class TenantLockTests
{
    private static readonly HashSet<string> AddMethods = new(StringComparer.Ordinal)
    {
        "Add", "AddAsync", "AddRange", "AddRangeAsync", nameof(AppDbContext.BulkInsertAsync),
    };

    // An add of an object, such as the AddRange of the context, has no entity type in the IL, so it can add a root.
    private static readonly string UntypedEntity = typeof(object).FullName!;

    [Test]
    public void each_type_that_adds_a_tenant_owned_root_adds_and_saves_in_the_shared_lock_of_its_tenant()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unlocked = AddsWithNoSharedLock(typeof(AppDbContext).Assembly, TenantOwnedRoots(context.Model));

        // Assert
        unlocked.ShouldBeEmpty(
            "An add of a tenant-owned root must not save during a tenant delete. Load and check the tenant or the parent, " +
            "add, and save in the work of RootLock.Tenant.RunSharedAsync, as AddAgent does. See docs/backend/concurrency.md, section \"Lock\".");
    }

    [Test]
    public void rule_finds_an_add_with_no_lock_through_a_set_the_context_a_range_or_a_bulk_insert_an_add_after_or_in_another_lock_and_a_save_after_the_lock()
    {
        // Arrange
        using var context = new AppDbContextFactory().CreateDbContext([]);

        // Act
        var unlocked = AddsWithNoSharedLock(typeof(FixtureMarker).Assembly, TenantOwnedRoots(context.Model));

        // Assert
        unlocked.ShouldBe(
        [
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddAfterTheSharedWork adds Agent",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddAfterTheSharedWork saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddInExclusiveWork adds Agent",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddInExclusiveWork saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddRangeThroughTheContextWithNoLock adds Object",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddRangeThroughTheContextWithNoLock saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddRangeWithNoLock adds User",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddRangeWithNoLock saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddThroughTheContextWithNoLock adds User",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddThroughTheContextWithNoLock saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddWithNoLock adds Agent",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.AddWithNoLock saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.BulkInsertWithNoLock adds Agent",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.SaveAfterTheSharedWork saves outside the shared lock",
            "FullstackTemplate.ArchitectureFixtures.Core.TenantLock.SaveSynchronouslyAfterTheSharedWork saves outside the shared lock",
        ]);
    }

    private static HashSet<string> TenantOwnedRoots(IModel model)
    {
        var auditModel = AuditModel.Of(model);
        return TenantOwnedRows.InLoadOrder(model)
            .Where(e => auditModel.OwnerKeyOf(e) is not { IsRequired: true })
            .SelectMany(e => e.GetDerivedTypesInclusive())
            .Select(e => e.ClrType.FullName!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string[] AddsWithNoSharedLock(Assembly assembly, HashSet<string> roots)
    {
        var module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(assembly.Location)));
        var methods = module.GetTypes().SelectMany(t => t.Methods).Where(m => m.HasBody).ToList();
        var sharedWork = methods.SelectMany(SharedWorkOf).ToHashSet();

        var adds = methods
            .SelectMany(method => method.Body.Instructions
                .Select(AddedEntityOf)
                .OfType<string>()
                .Select(entity => (Owner: Outermost(method.DeclaringType).FullName, Entity: entity, Method: method)))
            .ToList();
        var addsATenant = adds.Where(a => a.Entity == typeof(Tenant).FullName).Select(a => a.Owner).ToHashSet(StringComparer.Ordinal);

        return adds
            .Where(a => (roots.Contains(a.Entity) || a.Entity == UntypedEntity) && !addsATenant.Contains(a.Owner) && !sharedWork.Contains(BodyOwner(a.Method)))
            .Select(a => $"{a.Owner} adds {a.Entity[(a.Entity.LastIndexOf('.') + 1)..]}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    // The lambda of a RunSharedAsync call is the last method that the caller loads with ldftn before the call.
    private static IEnumerable<MethodDefinition> SharedWorkOf(MethodDefinition method)
    {
        foreach (var call in method.Body.Instructions.Where(IsRunShared))
        {
            var load = call.Previous;
            while (load is not null && load.OpCode.Code != Code.Ldftn)
                load = load.Previous;
            if ((load?.Operand as MethodReference)?.Resolve() is { } work)
                yield return work;
        }
    }

    private static bool IsRunShared(Instruction instruction)
        => instruction is { OpCode.Code: Code.Call or Code.Callvirt, Operand: MethodReference method }
           && method.Name == nameof(RootLock.RunSharedAsync)
           && method.DeclaringType.FullName == typeof(RootLock).FullName;

    private static string? AddedEntityOf(Instruction instruction)
    {
        if (instruction is not { OpCode.Code: Code.Call or Code.Callvirt, Operand: MethodReference method }
            || !AddMethods.Contains(method.Name))
            return null;
        if (method.DeclaringType is GenericInstanceType { ElementType.FullName: "Microsoft.EntityFrameworkCore.DbSet`1" } set)
            return set.GenericArguments[0].FullName;
        if (!IsContext(method.DeclaringType))
            return null;
        return method is GenericInstanceMethod { GenericArguments: [var entity] } ? entity.FullName : UntypedEntity;
    }

    private static bool IsContext(TypeReference type)
    {
        for (var current = type.Resolve(); current is not null; current = current.BaseType?.Resolve())
            if (current.FullName == "Microsoft.EntityFrameworkCore.DbContext")
                return true;
        return false;
    }

    // The body of an async lambda is the MoveNext of its state machine, so the add sits in a nested type of the lambda.
    private static MethodDefinition BodyOwner(MethodDefinition method)
    {
        if (method.DeclaringType.DeclaringType is not { } outer)
            return method;
        return outer.Methods.FirstOrDefault(m => m.CustomAttributes.Any(a =>
                   a.AttributeType.FullName == typeof(AsyncStateMachineAttribute).FullName
                   && a.ConstructorArguments[0].Value is TypeReference machine
                   && machine.FullName == method.DeclaringType.FullName))
               ?? method;
    }

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
            type = type.DeclaringType;
        return type;
    }
}
