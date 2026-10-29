namespace FullstackTemplate.FunctionalTests.FunctionalTests.Users;

using System.Net;
using System.Net.Http.Json;
using FullstackTemplate.Server.Domain.Agents;
using FullstackTemplate.Server.Domain.Agents.Dtos;
using FullstackTemplate.Server.Domain.Agents.Features;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using FullstackTemplate.SharedTestHelpers.Fakes.Agent;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using TestUtilities;

/// <summary>
/// A handler that loads a user or an agent without its permissions maps an empty list, and the response tells the
/// caller that the owner has no permissions (issue 99). Each request whose response has a type with a Permissions
/// property has a case here, and the case compares the permissions of the response with the rows in the database.
/// </summary>
public class PermissionsInResponseTests : TestBase
{
    private static readonly Dictionary<string, Func<Arranged, Task<Returned>>> Cases = new()
    {
        [Name(typeof(AddUser.Command))] = async a =>
        {
            var dto = new FakeUserForCreationDto().Generate() with { Role = UserRole.Admin().Value };
            var user = await ReadAsync<UserDto>(
                await a.Client.PostJsonRequestAsync(ApiRoutes.Users.Create, dto), HttpStatusCode.Created);
            return Returned.OfUser(user);
        },
        [Name(typeof(UpdateUser.Command))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers(), Permission.ReadAgents());
            var dto = new FakeUserForUpdateDto().Generate() with { Version = target.Version };
            return Returned.OfUser(await ReadAsync<UserDto>(
                await a.Client.PutJsonRequestAsync(ApiRoutes.Users.Put(target.Id), dto)));
        },
        [Name(typeof(UpdateUserRole.Command))] = async a =>
        {
            // The new role replaces each row, so a row that the handler did not load stays in the database
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers());
            return Returned.OfUser(await ReadAsync<UserDto>(await a.Client.PutJsonRequestAsync(
                ApiRoutes.Users.UpdateRole(target.Id), new UpdateUserRoleDto { Role = UserRole.Admin().Value })));
        },
        [Name(typeof(AddUserPermission.Command))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers());
            return Returned.OfUser(await ReadAsync<UserDto>(await a.Client.PostJsonRequestAsync(
                ApiRoutes.Users.AddPermission(target.Id), new UserPermissionDto { Permission = Permission.Names.ReadAgents })));
        },
        [Name(typeof(RemoveUserPermission.Command))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.Admin());
            return Returned.OfUser(await ReadAsync<UserDto>(await a.Client.DeleteRequestAsync(
                ApiRoutes.Users.RemovePermission(target.Id, Permission.Names.ReadAgents))));
        },
        [Name(typeof(GetUser.Query))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers(), Permission.ReadAgents());
            return Returned.OfUser(await ReadAsync<UserDto>(
                await a.Client.GetRequestAsync(ApiRoutes.Users.GetRecord(target.Id))));
        },
        [Name(typeof(GetUserByIdentifier.Query))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers(), Permission.ReadAgents());
            return Returned.OfUser(await ReadAsync<UserDto>(
                await a.Client.GetRequestAsync(ApiRoutes.Users.GetByIdentifier(target.Identifier))));
        },
        [Name(typeof(GetUserList.Query))] = async a =>
        {
            var target = await a.AddUserAsync(UserRole.User(), Permission.ReadUsers(), Permission.ReadAgents());
            var users = await ReadAsync<List<UserDto>>(await a.Client.GetRequestAsync(ApiRoutes.Users.GetList));
            return Returned.OfUser(users.Single(u => u.Id == target.Id));
        },
        [Name(typeof(InitiateUserAndTenant.Command))] = async a =>
        {
            var dto = new FakeInitiateUserAndTenantDto().Generate() with { Role = UserRole.Admin().Value };
            a.Client.SignInAsAppUser(dto.Identifier);
            var initiated = await ReadAsync<InitiateUserAndTenantResponseDto>(
                await a.Client.PostJsonRequestAsync(ApiRoutes.Users.Initiate, dto), HttpStatusCode.Created);
            return Returned.OfUser(initiated.User);
        },
        [Name(typeof(AddAgent.Command))] = async a =>
        {
            var dto = new FakeAgentForCreationDto().Generate() with
            {
                Permissions = [Permission.Names.ReadUsers, Permission.Names.ReadAgents],
            };
            return Returned.OfAgent(await ReadAsync<AgentDto>(
                await a.Client.PostJsonRequestAsync(ApiRoutes.Agents.Create, dto), HttpStatusCode.Created));
        },
        [Name(typeof(AddAgentPermission.Command))] = async a =>
        {
            var agent = await a.AddAgentAsync(Permission.Names.ReadUsers);
            return Returned.OfAgent(await ReadAsync<AgentDto>(await a.Client.PostJsonRequestAsync(
                ApiRoutes.Agents.AddPermission(agent.Id), new AgentPermissionDto { Permission = Permission.Names.ReadAgents })));
        },
        [Name(typeof(RemoveAgentPermission.Command))] = async a =>
        {
            var agent = await a.AddAgentAsync(
                Permission.Names.ReadUsers, Permission.Names.ReadAgents, Permission.Names.ManageTenant);
            return Returned.OfAgent(await ReadAsync<AgentDto>(await a.Client.DeleteRequestAsync(
                ApiRoutes.Agents.RemovePermission(agent.Id, Permission.Names.ManageTenant))));
        },
        [Name(typeof(GetAgent.Query))] = async a =>
        {
            var agent = await a.AddAgentAsync(Permission.Names.ReadUsers, Permission.Names.ReadAgents);
            return Returned.OfAgent(await ReadAsync<AgentDto>(
                await a.Client.GetRequestAsync(ApiRoutes.Agents.GetRecord(agent.Id))));
        },
        [Name(typeof(GetAgentList.Query))] = async a =>
        {
            var agent = await a.AddAgentAsync(Permission.Names.ReadUsers, Permission.Names.ReadAgents);
            var agents = await ReadAsync<List<AgentDto>>(await a.Client.GetRequestAsync(ApiRoutes.Agents.GetList));
            return Returned.OfAgent(agents.Single(x => x.Id == agent.Id));
        },
    };

    public static IEnumerable<string> RequestNames() => Cases.Keys;

    [Test]
    [MethodDataSource(nameof(RequestNames))]
    public async Task response_has_the_permissions_that_the_database_has(string request)
    {
        // Arrange
        var arranged = await ArrangeAsync();

        // Act
        var returned = await Cases[request](arranged);

        // Assert
        var stored = await returned.StoredAsync();
        stored.Count.ShouldBeGreaterThan(1, "The case must give the owner more than one permission.");
        returned.Permissions.ShouldBe(stored, ignoreOrder: true);
    }

    [Test]
    public void each_request_whose_response_has_permissions_has_a_case()
    {
        // Arrange
        var requests = typeof(UserDto).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .Where(t => ResponseTypesOf(t).Any(r => HasPermissions(r, [])))
            .Select(Name);

        // Act & Assert
        Cases.Keys.ShouldBe(requests, ignoreOrder: true,
            "Each request whose response has a Server type with a Permissions property needs a case in Cases.");
    }

    private static IEnumerable<Type> ResponseTypesOf(Type request)
        => request.GetInterfaces()
            .Where(i => i.IsGenericType
                        && (i.GetGenericTypeDefinition() == typeof(IRequest<>)
                            || i.GetGenericTypeDefinition() == typeof(IStreamRequest<>)))
            .Select(i => i.GetGenericArguments()[0]);

    private static bool HasPermissions(Type type, HashSet<Type> seen)
    {
        if (!seen.Add(type))
            return false;
        if (type.HasElementType)
            return HasPermissions(type.GetElementType()!, seen);
        if (type.IsGenericType && type.GetGenericArguments().Any(t => HasPermissions(t, seen)))
            return true;
        if (type.Assembly != typeof(UserDto).Assembly)
            return false;
        var properties = type.GetProperties();
        return properties.Any(p => p.Name == nameof(UserDto.Permissions))
               || properties.Any(p => HasPermissions(p.PropertyType, seen));
    }

    private static string Name(Type request) => $"{request.DeclaringType?.Name}.{request.Name}";

    private async Task<Arranged> ArrangeAsync()
    {
        var seed = TestSeed.Generate();
        await InsertAsync(seed.Tenant);
        await InsertAsync(seed.User);
        FactoryClient.SignInAsAppUser(seed.User.Identifier);
        return new Arranged(FactoryClient, seed);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
        where T : class
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>()).ShouldNotBeNull();
    }

    private sealed record Arranged(HttpClient Client, TestSeed Seed)
    {
        public async Task<User> AddUserAsync(UserRole role, params Permission[] permissions)
        {
            TestGraph.Start(Seed, NullStore.Instance).AddUser(out var user, u => u.Role = role.Value);
            user.AddPermissions(permissions);
            await InsertAsync(user);
            return user;
        }

        public async Task<Agent> AddAgentAsync(params string[] grant)
        {
            TestGraph.Start(Seed, NullStore.Instance).AddAgent(out var agent, x => x.Permissions = grant);
            await InsertAsync(agent);
            return agent;
        }
    }

    private sealed record Returned(IReadOnlyList<string> Permissions, Func<Task<List<string>>> StoredAsync)
    {
        public static Returned OfUser(UserDto user)
            => new(user.Permissions, () => ExecuteDbContextAsync(db => db.UserPermissions
                .Where(p => p.UserId == user.Id)
                .Select(p => p.Permission.Value)
                .ToListAsync()));

        public static Returned OfAgent(AgentDto agent)
            => new(agent.Permissions, () => ExecuteDbContextAsync(db => db.AgentPermissions
                .Where(p => p.AgentId == agent.Id)
                .Select(p => p.Permission.Value)
                .ToListAsync()));
    }
}
