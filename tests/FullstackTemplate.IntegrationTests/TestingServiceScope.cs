namespace FullstackTemplate.IntegrationTests;

using System.Security.Claims;
using Bogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class TestingServiceScope
{
    private const string AuthenticationType = "TestAuth";

    private readonly IServiceScope _scope;

    public TestingServiceScope() : this(TestFixture.BaseScopeFactory)
    {
    }

    public TestingServiceScope(IServiceProvider services) : this(services.GetRequiredService<IServiceScopeFactory>())
    {
    }

    private TestingServiceScope(IServiceScopeFactory scopeFactory)
    {
        _scope = scopeFactory.CreateScope();

        // Set up ICurrentUserService mock to use the default user's identifier
        // The real ITenantIdProvider will look up this user and return their tenant
        SetUserWithDefaults();
    }

    public TScopedService GetService<TScopedService>() where TScopedService : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<TScopedService>();
    }

    /// <summary>
    /// Sets up ICurrentUserService mock with the default user's identifier, signed in through the first-party client.
    /// The real ITenantIdProvider will look up this user to get their tenant.
    /// </summary>
    public void SetUserWithDefaults(
        string? identifier = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null)
    {
        var faker = new Faker();
        var currentUserService = GetService<ICurrentUserService>();

        // Use the default user's identifier so ITenantIdProvider can look them up
        var userIdentifier = identifier ?? TestFixture.DefaultUser.Identifier;

        currentUserService.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", userIdentifier), new Claim("azp", TestFixture.FirstPartyClientId)], AuthenticationType)));
        currentUserService.UserIdentifier.Returns(userIdentifier);
        currentUserService.ClientId.Returns(TestFixture.FirstPartyClientId);
        currentUserService.Email.Returns(email ?? faker.Internet.Email());
        currentUserService.FirstName.Returns(firstName ?? faker.Person.FirstName);
        currentUserService.LastName.Returns(lastName ?? faker.Person.LastName);
        currentUserService.Username.Returns(faker.Internet.UserName());
        currentUserService.IsAuthenticated.Returns(true);
        currentUserService.Roles.Returns(new List<string>());
        RefreshTenantScope();
    }

    /// <summary>
    /// Sets up a user with specific roles.
    /// </summary>
    public void SetUserWithRoles(params string[] roles)
    {
        var currentUserService = GetService<ICurrentUserService>();
        currentUserService.Roles.Returns(roles.ToList());

        foreach (var role in roles)
        {
            currentUserService.IsInRole(role).Returns(true);
        }
    }

    /// <summary>
    /// Sets up an anonymous HTTP request: a principal with an unauthenticated identity and no claims.
    /// </summary>
    public void SetUserUnauthenticated()
    {
        var currentUserService = GetService<ICurrentUserService>();

        currentUserService.User.Returns(new ClaimsPrincipal(new ClaimsIdentity()));
        currentUserService.IsAuthenticated.Returns(false);
        currentUserService.UserIdentifier.Returns((string?)null);
        currentUserService.ClientId.Returns((string?)null);
        RefreshTenantScope();
    }

    /// <summary>
    /// Sets up a machine-to-machine client: an authenticated principal with a "client_id" claim. It has no "sub",
    /// or the <paramref name="serviceAccountSubject"/> that Keycloak, ZeroFactor, and Cognito put in such a token.
    /// </summary>
    public void SetMachineClient(string clientId, string? serviceAccountSubject = null)
    {
        Claim[] claims = serviceAccountSubject is null
            ? [new Claim("client_id", clientId)]
            : [new Claim("client_id", clientId), new Claim("sub", serviceAccountSubject)];
        SetToken(claims, serviceAccountSubject, clientId);
    }

    /// <summary>
    /// Sets up a user token that a client that is not first-party got for the user with this identifier, as an agent
    /// that acts for the user sends it.
    /// </summary>
    public void SetDelegatedClient(string userIdentifier, string clientId)
        => SetToken([new Claim("sub", userIdentifier), new Claim("azp", clientId)], userIdentifier, clientId);

    private void SetToken(Claim[] claims, string? subject, string clientId)
    {
        var currentUserService = GetService<ICurrentUserService>();

        currentUserService.User.Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType)));
        currentUserService.ClientId.Returns(clientId);
        currentUserService.UserIdentifier.Returns(subject);
        currentUserService.IsAuthenticated.Returns(true);
        RefreshTenantScope();
    }

    // AppDbContext keeps the tenant scope of its caller. A test that changes the caller of this scope
    // must make the context get the scope again.
    private void RefreshTenantScope() => GetService<AppDbContext>().RefreshTenantScope();

    /// <summary>
    /// Starts a scenario on the seed tenant. <c>BuildAsync</c> saves the new entities in one save
    /// and clears the change tracker, so the code under test loads them fresh.
    /// </summary>
    public GraphBuilder<TestSeed, Tenant[]> StartGraph(out TestSeed seed)
    {
        seed = TestFixture.Seed;
        return TestGraph.Start(seed, GetService<AppDbContext>().AsGraphStore());
    }

    /// <summary>
    /// Creates a new tenant and user in the database, then sets the current user to that user.
    /// Use this when you need to test with a different tenant than the default.
    /// </summary>
    public async Task<Tenant> SetupTenantAsync(string? name = null)
    {
        await StartGraph(out _)
            .AddTenant(out var tenant, t => t.Name = name)
            .AddUser(out var user)
            .BuildAsync();

        // Update ICurrentUserService to use this user's identifier
        SetUserWithDefaults(identifier: user.Identifier);

        return tenant;
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        var mediator = _scope.ServiceProvider.GetRequiredService<ISender>();
        return await mediator.Send(request);
    }

    public async Task SendAsync(IRequest request)
    {
        var mediator = _scope.ServiceProvider.GetRequiredService<ISender>();
        await mediator.Send(request);
    }

    public async Task<TEntity?> FindAsync<TEntity>(params object[] keyValues)
        where TEntity : class
    {
        var context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.FindAsync<TEntity>(keyValues);
    }

    public async Task AddAsync<TEntity>(TEntity entity)
        where TEntity : class
    {
        var context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Add(entity);
        await context.SaveChangesAsync();
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        return await action(_scope.ServiceProvider);
    }

    public Task<T> ExecuteDbContextAsync<T>(Func<AppDbContext, Task<T>> action)
        => ExecuteScopeAsync(sp => action(sp.GetRequiredService<AppDbContext>()));

    public async Task<int> InsertAsync<T>(params T[] entities) where T : class
    {
        return await ExecuteDbContextAsync(async db =>
        {
            foreach (var entity in entities)
            {
                db.Set<T>().Add(entity);
            }
            return await db.SaveChangesAsync();
        });
    }
}
