# Domain events

A domain event tells the rest of the system that an entity changed. The entity queues the event. `AppDbContext.SaveChangesAsync` saves the changes and then publishes each queued event with MediatR. The examples come from `Domain/Users/DomainEvents/`.

## Rules

1. Put events and handlers in `Domain/[EntityName]s/DomainEvents/`.
2. Make each event a `sealed record` that derives from `DomainEvent`.
3. Name an event in the past tense, for example `UserCreated` or `OrderSubmitted`.
4. Queue the event in the entity method that changes the state. Do not create an event in a handler.
5. Do not call `mediator.Publish` for a domain event. `SaveChangesAsync` publishes it.
6. Do not add an event for a delete. The context does soft deletes.
7. Do not use an event for validation. Validate in the entity.

## Events

A created event carries the entity. An updated event carries the ID. A business event carries the data that its handlers need, so that the handlers do not have to query for it.

```csharp
public sealed record UserCreated(User User) : DomainEvent;

public sealed record UserUpdated(string Id) : DomainEvent;

public sealed record OrderSubmitted(string OrderId, string CustomerId, decimal TotalAmount) : DomainEvent;
```

The entity queues the event:

```csharp
user.QueueDomainEvent(new UserCreated(user));
```

## Handlers

A handler implements `INotificationHandler<TEvent>`. Inject services with a primary constructor. One event can have many handlers. Make each handler independent of the others, and make it safe to run two times.

```csharp
/// <summary>
/// Removes the cached tenant lookup for the new user's identifier. The cache can hold a miss
/// for this identifier, and the user must see their tenant on the next request.
/// </summary>
public sealed class UserCreatedHandler(ITenantIdProvider tenantIdProvider) : INotificationHandler<UserCreated>
{
    public async Task Handle(UserCreated notification, CancellationToken cancellationToken)
    {
        await tenantIdProvider.InvalidateCacheAsync(notification.User.Identifier, cancellationToken);
    }
}
```

The handlers run after the save, in the same request. When a handler throws an exception, the saved changes stay in the database.
