namespace FullstackTemplate.SharedTestHelpers.EntityGraphs;

using Bogus;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Tenants.Models;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Models;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;

/// <summary>
/// One blueprint per entity. Each factory calls the real domain factory, so every built entity
/// passes the same invariants as production code. Never set entity state any other way.
/// </summary>
/// <remarks>
/// <see cref="GraphGraftAttribute"/> generates <c>AddTenant</c>, <c>AddTenants</c>, <c>AddUser</c>, and
/// <c>AddUsers</c> in <c>BlueprintsGrafts</c>. Child methods compile only after their parent type.
/// Each factory passes the input <c>Id</c> to the creation model. The input <c>Id</c> is null by default, so the entity
/// makes the ID. Set it to give a fixed ID. The domain factory rejects an ID with the wrong prefix.
/// </remarks>
[GraphGraft]
public static class Blueprints
{
    private static readonly Faker Faker = new();

    public static readonly InputBlueprint<TestSeed, TenantInput, Tenant> Tenant = Blueprint
        .FromInput<TestSeed, TenantInput, Tenant>(
            () => new(),
            (input, _) => Server.Domain.Tenants.Tenant.Create(new TenantForCreation
            {
                Id = input.Id,
                Name = input.Name!,
            }))
        .Derive(i => i.Name, (_, _) => TenantRules.Name(Faker));

    /// <summary>A user is a child of a tenant. The tenant id comes from the parent, never from the test.</summary>
    public static readonly ChildInputBlueprint<TestSeed, Tenant, UserInput, User> User = Blueprint
        .ChildFromInput<TestSeed, Tenant, UserInput, User>(
            () => new(),
            (tenant, input, _) => Server.Domain.Users.User.Create(new UserForCreation
            {
                Id = input.Id,
                TenantId = tenant.Id,
                FirstName = input.FirstName!,
                LastName = input.LastName!,
                Identifier = input.Identifier!,
                Email = input.Email!,
                Username = input.Username!,
                Role = input.Role!,
            }))
        .Derive(i => i.FirstName, (_, _) => UserRules.FirstName(Faker))
        .Derive(i => i.LastName, (_, _) => UserRules.LastName(Faker))
        .Derive(i => i.Identifier, (_, _) => UserRules.Identifier(Faker))
        .Derive(i => i.Email, (_, _) => UserRules.Email(Faker))
        .Derive(i => i.Username, (_, _) => UserRules.Username(Faker))
        .Derive(i => i.Role, (_, _) => UserRules.Role(Faker));
}
