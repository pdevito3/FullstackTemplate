namespace FullstackTemplate.ArchitectureFixtures.DomainEvents;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Domain;
using FullstackTemplate.Server.Domain.Tenants;
using FullstackTemplate.Server.Domain.Users;
using MediatR;
using Microsoft.Extensions.Options;

public sealed class FixtureOptions
{
    public int Limit { get; set; }
}

public sealed record CleanEvent(string UserId, IReadOnlyList<string> Roles, DateTimeOffset? On) : DomainEvent;

public sealed record CleanEventOfInternalHandler(string UserId) : DomainEvent;

public record UnsealedEvent(string UserId) : DomainEvent;

public abstract record AbstractEvent(string UserId) : DomainEvent;

public sealed record EventWithEntity(User User) : DomainEvent;

public sealed record EventWithEntityTypeArgument(IReadOnlyList<User> Users) : DomainEvent;

public sealed record EventWithEntityArray(Tenant[] Tenants) : DomainEvent;

public sealed record EventWithEntityField : DomainEvent
{
    public Tenant? Tenant;
}

public sealed class CleanHandler(TimeProvider timeProvider, IOptions<FixtureOptions> options)
    : IDomainEventHandler<CleanEvent>
{
    public TimeProvider TimeProvider { get; } = timeProvider;
    public IOptions<FixtureOptions> Options { get; } = options;

    public Task Handle(CleanEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class CleanInternalHandler : IDomainEventHandler<CleanEventOfInternalHandler>
{
    public Task Handle(CleanEventOfInternalHandler domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public class UnsealedHandler : IDomainEventHandler<CleanEvent>
{
    public Task Handle(CleanEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class HandlerOfAbstractEvent : IDomainEventHandler<AbstractEvent>
{
    public Task Handle(AbstractEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class HandlerWithServiceProvider(IServiceProvider serviceProvider) : IDomainEventHandler<CleanEvent>
{
    public IServiceProvider ServiceProvider { get; } = serviceProvider;

    public Task Handle(CleanEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed class HandlerWithSecondConstructor : IDomainEventHandler<CleanEvent>
{
    public HandlerWithSecondConstructor() { }

    public HandlerWithSecondConstructor(HttpClient httpClient) => HttpClient = httpClient;

    public HttpClient? HttpClient { get; }

    public Task Handle(CleanEvent domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public sealed record NotificationWithMediatR : INotification;

public sealed class NotificationHandlerWithMediatR : INotificationHandler<NotificationWithMediatR>
{
    public Task Handle(NotificationWithMediatR notification, CancellationToken cancellationToken) => Task.CompletedTask;
}
