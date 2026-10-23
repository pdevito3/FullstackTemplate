namespace FullstackTemplate.IntegrationTests.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using FullstackTemplate.Server.Services;
using FullstackTemplate.SharedTestHelpers.EntityGraphs;
using FullstackTemplate.SharedTestHelpers.Fakes.User;
using Microsoft.Extensions.DependencyInjection;

public sealed class TestHandler<TEvent>(Func<TEvent, AppDbContext, CancellationToken, Task> handle)
    : IDomainEventHandler<TEvent>
    where TEvent : DomainEvent
{
    public Task Handle(TEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => handle(domainEvent, dbContext, cancellationToken);
}

public sealed record TenantScenario(Tenant Tenant, User Admin, User FirstUser, User SecondUser);

public static class DomainEventTestServices
{
    public static IServiceCollection AddTestHandler<TEvent>(
        this IServiceCollection services, Func<TEvent, AppDbContext, CancellationToken, Task> handle)
        where TEvent : DomainEvent
        => services.AddScoped<IDomainEventHandler<TEvent>>(_ => new TestHandler<TEvent>(handle));

    public static async Task<TenantScenario> StartTenantScenarioAsync(this TestingServiceScope scope)
    {
        await scope.StartGraph(out _)
            .AddTenant(out var tenant)
            .AddUsers(out var users, u => u.Role = "Admin", u => u.Role = "User", u => u.Role = "User")
            .BuildAsync();
        scope.SetUserWithDefaults(identifier: users[0].Identifier);
        return new TenantScenario(tenant, users[0], users[1], users[2]);
    }

    public static async Task<T> ReadAsync<T>(Func<AppDbContext, Task<T>> read)
    {
        await using var scope = TestFixture.BaseScopeFactory.CreateSystemScope();
        return await read(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public static User NewUser(string tenantId, string identifier) => User.Create(new FakeUserForCreation()
        .RuleFor(u => u.TenantId, _ => tenantId)
        .RuleFor(u => u.Identifier, _ => identifier)
        .RuleFor(u => u.Role, _ => "User")
        .Generate());
}
