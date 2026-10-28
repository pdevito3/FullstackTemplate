# Domain events

A domain event tells the rest of the system that an entity changed. The entity queues the event. `AppDbContext.SaveChangesAsync` runs the handlers of each queued event before it sends any SQL. The change and the handler changes then commit together. [ADR 0010](../adr/0010-domain-events-in-the-save-transaction.md) gives the reasons for this design.

## The guarantee

A save commits the change and every handler change in one database transaction, or it commits nothing.

- If a handler throws an exception, the save writes nothing and the caller gets the exception. A client error, such as a `ValidationException`, gives a 4xx response. Any other exception gives a 500.
- If the save fails with a 409 conflict, the save writes nothing, also the handler changes. A retry of the request runs the handlers again from the start.
- If the database has a transient failure, EF sends the SQL again. The handlers do not run again.
- A handler never runs after the commit. So a saved change never loses the effect of its handler.

For example, an inspection fails, and a handler takes the vehicle out of service. The failed inspection and the out-of-service vehicle commit together, or neither commits. No reader sees a failed inspection with the vehicle still in service.

The guarantee has two limits:

- A handler reads before the save transaction starts. If another request changes the same row between the read and the commit, the last save wins. Feature code has the same limit.
- If the connection drops during the commit, the app cannot know if the commit succeeded. EF sends the SQL again, and an insert then fails on its primary key. The caller gets a 409 for a save that committed, and a resend of the request doubles the effect.

## Rules

1. Put an event in `Domain/[EntityName]s/DomainEvents/` of the entity that queues it. Put a handler in `Domain/[EntityName]s/DomainEvents/` of the entity that it changes.
2. Make each event a `sealed record` that derives from `DomainEvent`.
3. Name an event in the past tense, for example `UserCreated` or `InspectionFailed`.
4. Put IDs and values in an event, not entities. `DomainEventShapeTests` fails on an event property that is an entity.
5. Queue the event in the entity method that changes the state. `QueueDomainEvent` is protected, so only the entity can queue its events.
6. Make each handler a `sealed class` that implements `IDomainEventHandler<TEvent>`. Change entities through the `dbContext` parameter of `Handle`.
7. Do not call `SaveChangesAsync` in a handler. The save that runs the handler saves its changes.
8. Inject only `TimeProvider` and `IOptions<T>` into a handler. `DomainEventShapeTests` fails on any other constructor parameter.
9. Read with LINQ in a handler. Do not use `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, raw SQL (`FromSql`, `SqlQuery`, or `ExecuteSql`), or a transaction. These run outside the save. `WriteGuard` refuses them while handlers run, also on a table with no audit. It cannot see a command that you send on `Database.GetDbConnection()`, so the build bans that call in the Server. See [audit-log.md](audit-log.md#writes-that-skip-the-save).
10. Do not send an email, call an HTTP API, or change a cache in a handler. See [Effects outside the database](#effects-outside-the-database).
11. Add an event for a delete only when a record of another domain holds the ID of the entity and must change with the delete. For example, a technician links to a user. A soft delete keeps the row, but the query filter hides it. The link then finds no entity. A record that only keeps history, such as an audit entry, does not count. The template queues `UserDeleted`, because apps often link records to a user. Do not add an event for other deletes.
    - Queue the event in an entity method, such as `User.Delete`. Call the method in the feature that removes the entity.
    - In a handler of the event, clear or mark the link. The change commits in the same save. The handler runs before the save writes the delete, so a query in the handler still finds the entity.
    - Stop the race between the delete and a link add with a `RootLock`. The feature that adds a link, or moves a link to another entity, does not write the row of the linked entity. So the version check does not stop its race with the delete. Run each delete in `RunExclusiveAsync`, and run the link add in `RunSharedAsync`. In the work of the add, check that the entity exists. A command takes one lock (rule 6 of [concurrency.md](concurrency.md#lock)). So when the delete of a parent root also deletes the entity under the lock of that root, use the lock of the parent, not a new lock.
    - For a user, the lock is `RootLock.Tenant` of the tenant of the user. `DeleteTenant` deletes each user of the tenant in `RunExclusiveAsync` of the tenant. A link add to a user runs in `RunSharedAsync` of the tenant of that user. Then `DeleteTenant` needs no second lock. Do not add a `RootLock.User`. `DeleteUser` takes no lock yet, because no template record links to a user. When an app adds the first link to a user, `DeleteUser` runs its load and its save in `RunExclusiveAsync` of the tenant of the user. Read the `TenantId` of the user before the lock, then load the user again in the work and check that it is in that tenant.
12. Do not use an event to validate the entity that queues it. Validate in the entity.
13. Do not open a transaction around `SaveChangesAsync`. One save is one transaction, and the context refuses an outer transaction.

## Events

An event carries the ID of the entity, and the values that its handlers need:

```csharp
public sealed record UserCreated(string UserId) : DomainEvent;

public sealed record InspectionFailed(string InspectionId, string VehicleId) : DomainEvent;
```

The entity method queues the event:

```csharp
public Inspection Fail(string finding)
{
    Status = InspectionStatus.Failed;
    Finding = finding;
    QueueDomainEvent(new InspectionFailed(Id, VehicleId));
    return this;
}
```

The feature calls the entity method and saves. It does not know about the handler:

```csharp
inspection.Fail(request.Finding);
await dbContext.SaveChangesAsync(cancellationToken);
```

## Handlers

A handler gets the event and the `AppDbContext` of the save. It loads the entities that it changes and calls their methods:

```csharp
/// <summary>
/// A vehicle that failed an inspection must not stay in service.
/// </summary>
public sealed class TakeVehicleOutOfServiceWhenInspectionFails : IDomainEventHandler<InspectionFailed>
{
    public async Task Handle(InspectionFailed domainEvent, AppDbContext dbContext, CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles
            .TagWithOperationCallSite("vehicles.out_of_service.load")
            .GetById(domainEvent.VehicleId, cancellationToken);

        vehicle.TakeOutOfService();
    }
}
```

The context finds each handler in the DI container. You do not register a handler.

- A handler runs in the tenant of the entity that queued the event. The tenant query filters in the handler show the rows of this tenant only, also when the caller is system work or a machine client that sees all tenants. When a handler adds an entity, set its `TenantId` from a loaded entity of the event.
- The tenant of the event is the `TenantId` of an `ITenantable` entity, or the `Id` of a `Tenant`. An entity with no tenant has no tenant to give, so the handlers of its events run in the tenant scope of the caller. An entity that gets its tenant through `ApplyTenanting` cannot queue a domain event, and the save throws a `NotSupportedException`.
- One event can have many handlers. Make each handler independent of the others. The order of the handlers of one event is not defined.
- The events run in the order that the entities queued them.
- A handler can change an entity whose method queues a new event. The new event runs in the same save. After 10 rounds of new events, the save throws an exception, because the handlers then form a cycle.
- A query in a handler reads the committed rows. It does not see the rows that this save adds. To get an entity that this save adds, use `FindAsync` or `DbSet.Local`. These two do not apply the tenant filter to the entities that the context tracks. If the save can have entities of more than one tenant, compare the `TenantId` of the entity with the tenant of the event.
- To change a child of an aggregate, such as a `UserPermission` of a `User`, load the root with a tracked query and change the child through the root. The save then changes the version of the root, so a caller that loaded the root before gets a 409, or runs again if its command has no version. If the context does not track the root, the save throws an `InvalidOperationException` and writes nothing. See [concurrency.md](concurrency.md#versions).
- To refuse the change, throw a domain exception, for example a `ValidationException`. The save then writes nothing.

## Errors

The context refuses these saves with an exception that names the fix:

| Cause | Exception |
|-------|-----------|
| A handler calls `SaveChangesAsync` | `InvalidOperationException` |
| A save on a context whose earlier save failed | `InvalidOperationException`. Use a new DI scope. |
| A save inside a transaction that you opened | `NotSupportedException` |
| A handler runs a command that is not a LINQ query, such as `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, or raw SQL | `InvalidOperationException` |
| A handler begins a transaction, or uses one with `UseTransaction` | `InvalidOperationException` |
| An entity that gets its tenant through `ApplyTenanting` queues a domain event | `NotSupportedException` |
| A call to `SaveChanges`, the sync version | `NotSupportedException`. Use `SaveChangesAsync`. |
| Handlers queue new events for more than 10 rounds | `InvalidOperationException`. The message names the event types. |
| A handler changes a child of a root that the context does not track | `InvalidOperationException`. Load the root, then change the child through it. |

A failed save ends the context, also a failure of EF's own save, such as a 409 conflict. The change tracker still has the handler changes of the failed try. A second save could keep a change that a handler made from old data, or run the handlers again and double the effect. So do not catch the exception and save again. The caller sends the request again, and the new request runs in a new DI scope.

## Effects outside the database

A handler runs before the commit. Work outside the database cannot roll back, so it is not handler work.

- **The tenant lookup cache.** You do not do anything. The context removes the stale lookups after each commit. See [multi-tenancy.md](../multi-tenancy.md#tenant-lookup).
- **An email, a webhook, or a message to another system.** In a handler, add an entity that asks for the work, for example `WelcomeEmailRequest`. The request commits with the change, so it is never lost. A background service then does the work after the commit, at least one time. It sends the ID of the request as the idempotency key, because it can do the work two times after a crash. The template does not have this service yet. Add it with the first feature that needs it, in the shape that ADR 0010 gives.

Do not do the work after `SaveChangesAsync` returns when the work must happen. A crash or a failure after the commit loses it.

## Telemetry

Each handler run has a `domain_event.handle` span. It is a child of the feature span, so the trace leads from the request to the handler and to its queries. The `app.domain_event.type` and `app.domain_event.handler` tags have the type names only. When a handler fails with a server error, the span has the error status and the exception. See [telemetry.md](../telemetry.md).

## Tests

- To test an entity method, check `entity.DomainEvents` after the call.
- To test a handler, send the command of the feature that queues the event. Then read the database in a new scope. The integration test fixture has all the handlers of the Server.
