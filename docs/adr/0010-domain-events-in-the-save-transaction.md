# Domain event handlers run in the save transaction

`AppDbContext.SaveChangesAsync` runs the handlers of the queued domain events before it sends any SQL. The change and the handler changes commit in one database transaction, or nothing commits. A handler changes entities only through the context of the save, and it cannot call another system. Each handler runs in the tenant of the entity that queued the event. MediatR does not dispatch domain events. The tenant lookup cache is not a handler. The context removes the stale lookups after the commit, before `SaveChangesAsync` returns. Before this decision, the handlers ran after the commit with no retry. Two apps built on this template lost a handler effect after a saved change. One could leave an unsafe vehicle in service, and one lost a no-show fee.

## Considered options

- **A transactional outbox for all handlers.** The save writes each event to a table in the same transaction, and a background service runs the handlers after the commit, with retries. We did not choose this option, because the effect comes after the commit. A failed inspection then shows a vehicle in service until a retry succeeds, and an event that always fails keeps the vehicle in service. Each handler must also be safe to run two times. The template has no effect outside the database that needs this machinery.
- **Two kinds of handler: in the transaction for database changes, and an outbox for other effects.** We did not choose this option now. The outbox kind has no handler in the template, and each author must pick the correct kind. The request entity pattern below adds the outbox part when the first effect outside the database comes.
- **A handler kind that runs after the commit with no retry, for caches.** We did not choose this option. It is the path that lost the no-show fee, and authors copy the nearest example. The only cache that needs it is the tenant lookup cache, and the context removes its stale lookups without a handler.
- **MediatR `Publish` inside an explicit transaction.** We did not choose this option. Handlers keep constructor injection, so they can call an HTTP client or another `DbContext`. A feature can call `Publish` directly. Each save also needs a transaction inside the retrying execution strategy.
- **Keep `Publish` after the commit, and document it as best effort.** We did not choose this option, because it keeps the defect.
- **Handlers run in the tenant scope of the caller.** We did not choose this option. System work and a machine client see all tenants, so the tenant query filters in the handler then show the rows of all tenants. A new user with no user record sees no tenant, so a handler of `TenantCreated` sees no rows.
- **Each event names its tenant.** We did not choose this option. Each author must give the tenant, and a wrong value moves the handler to a different tenant. The entity that queues the event already has the tenant.
- **A context that stays usable after a failed save.** EF keeps the tracked changes after a failed save, so a second save can commit them. We did not choose this option. After a 409 conflict, the tracker still has the handler changes of the failed try. A retry on the same context then keeps a change that a handler made from old data, or runs the handlers again and doubles the effect.

## Consequences

- A handler failure fails the request, and the save writes nothing. The caller can send the request again.
- A failed save ends the context, also a save that fails with a 409 conflict. A later save on the same context throws an exception. A retry of the request runs in a new DI scope, so the handlers run again on a clean change tracker, and no change from the failed try stays.
- Each handler runs one time for each committed save. A transient database failure makes EF send the SQL again, but the handlers do not run again.
- A lost acknowledgment of the commit is a known limit. EF cannot know that the commit succeeded, so it sends the SQL again, and an insert fails on its primary key. The caller gets a 409 for a save that committed, and a resend of the request doubles the effect. This decision does not close that gap. A fix reports a primary key conflict as an unknown result, not as a 409.
- The tenant query filters in a handler show the rows of the tenant of the event only, also when the caller can see all tenants or no tenant. The tenant of the event is the `TenantId` of an `ITenantable` entity, or the `Id` of a `Tenant`. An entity with no tenant has no tenant to give, so the handlers of its events run in the tenant scope of the caller. An entity that gets its tenant through `ApplyTenanting` cannot queue a domain event, and the save throws a `NotSupportedException`.
- A slow handler makes the request slow. Long work goes to a request entity, the same as an effect outside the database.
- The context refuses a sync save, a save in a handler, a save after a failed save, and a save inside an outer transaction. While handlers run, it refuses each command that is not a LINQ query, and each transaction. It cannot see a command sent on the raw database connection.
- `DeleteUser` and `DeleteTenant` do not clear the tenant lookup cache themselves. Each save that adds a user, deletes a user, or deletes a tenant clears the stale lookups on its server instance. Other instances still keep the old result until it expires, because the cache has no backplane.
- For an email, a webhook, or a message to another system, a handler adds a request entity in the transaction. The first feature that needs one builds the background sender in this shape:
  - It claims due rows with `FOR UPDATE SKIP LOCKED`, so two instances do not claim the same row.
  - It counts the attempt when it claims the row, so a crash during the work still counts.
  - It marks the row done in the same transaction as the database part of the work, inside the execution strategy.
  - It sends the ID of the row as the idempotency key.
  - After a fixed number of attempts, it marks the row dead and records an error, so the row does not block other rows.
