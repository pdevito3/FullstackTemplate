namespace FullstackTemplate.IntegrationTests;

using Bogus;
using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

public class TestingServiceScope
{
    private readonly IServiceScope _scope;

    public TestingServiceScope()
    {
        _scope = TestFixture.BaseScopeFactory.CreateScope();

        // Set up default authenticated user
        SetUserWithDefaults();
    }

    public TScopedService GetService<TScopedService>() where TScopedService : notnull
    {
        return _scope.ServiceProvider.GetRequiredService<TScopedService>();
    }

    /// <summary>
    /// Sets up a default authenticated user with basic claims using fake data.
    /// </summary>
    public void SetUserWithDefaults(
        string? identifier = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null)
    {
        var faker = new Faker();
        var currentUserService = GetService<ICurrentUserService>();

        currentUserService.UserIdentifier.Returns(identifier ?? Guid.NewGuid().ToString());
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
