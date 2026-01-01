namespace FullstackTemplate.IntegrationTests;

using Bogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.Fakes.Tenant;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class TestingServiceScope
{
    private readonly IServiceScope _scope;

    public TestingServiceScope()
    {
        _scope = TestFixture.BaseScopeFactory.CreateScope();

        // Set up ICurrentUserService mock to use the default user's identifier
        // The real ITenantIdProvider will look up this user and return their tenant
        SetUserWithDefaults();
    }

    public TScopedService GetService<TScopedService>() where TScopedService : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<TScopedService>();
    }

    /// <summary>
    /// Sets up ICurrentUserService mock with the default user's identifier.
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

        currentUserService.UserIdentifier.Returns(userIdentifier);
        currentUserService.Email.Returns(email ?? faker.Internet.Email());
        currentUserService.FirstName.Returns(firstName ?? faker.Person.FirstName);
        currentUserService.LastName.Returns(lastName ?? faker.Person.LastName);
        currentUserService.Username.Returns(faker.Internet.UserName());
        currentUserService.IsAuthenticated.Returns(true);
        currentUserService.IsMachine.Returns(false);
        currentUserService.Roles.Returns(new List<string>());
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
    /// Sets up an unauthenticated user.
    /// </summary>
    public void SetUserUnauthenticated()
    {
        var currentUserService = GetService<ICurrentUserService>();

        currentUserService.IsAuthenticated.Returns(false);
        currentUserService.UserIdentifier.Returns((string?)null);
    }

    /// <summary>
    /// Sets up a machine-to-machine client.
    /// </summary>
    public void SetMachineClient(string clientId)
    {
        var currentUserService = GetService<ICurrentUserService>();
        currentUserService.IsMachine.Returns(true);
        currentUserService.ClientId.Returns(clientId);
        currentUserService.UserIdentifier.Returns((string?)null);
        currentUserService.IsAuthenticated.Returns(true);
    }

    /// <summary>
    /// Creates a new tenant and user in the database, then sets the current user to that user.
    /// Use this when you need to test with a different tenant than the default.
    /// </summary>
    public async Task<Tenant> SetupTenantAsync(string? name = null)
    {
        var tenant = new FakeTenantBuilder()
            .WithName(name ?? new Faker().Company.CompanyName())
            .Build();

        await InsertAsync(tenant);

        // Create a user in this tenant so ITenantIdProvider can look them up
        var user = new FakeUserBuilder()
            .WithTenantId(tenant.Id)
            .WithIdentifier($"tenant-user-{Guid.NewGuid()}")
            .Build();

        await InsertAsync(user);

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
