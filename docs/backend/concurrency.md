# Concurrency

Two requests can change the same rows at the same time. Each command picks one of three tools for this race: a version, a run again, or a lock. Pick the tool before you write the handler.

| Tool | Use it when | A lost race gives |
|------|-------------|-------------------|
| A version | The user saw data and decided on it, for example in an edit form | 409 stale version. The caller loads the record again and repeats the change |
| A run again | The command sends no version, and the server decides it from the rows alone | Nothing. The command runs again on the new rows. After 20 lost runs, 409 concurrent change |
| A lock | The rule of the command reads rows that the command does not write | Nothing. The second request waits for the first |

A create of a new aggregate root writes no row that another request can change, so it needs no version and no run again. It needs a lock only when its rule reads other rows, such as a booking that checks its clinic. A unique value that two creates add at the same time gets a 409 from the unique index (see [Error responses](../backend.md#error-responses)).

## Versions

Each entity has `Version`, the Postgres `xmin` of its row. `MapVersionToXmin` maps it for each entity, so a new entity needs no configuration for it. EF Core adds `WHERE xmin = @loaded` to each update. When another save changed the row after the load, the save throws a `StaleVersionException`. The reasons for this design are in [ADR 0008](../adr/0008-optimistic-concurrency-with-xmin.md).

- A save that adds, changes, or removes an owned child also updates the owner row. Thus the version of the root guards the whole aggregate. An owned child is the dependent of a `Cascade` foreign key (see [entity-configurations.md](entity-configurations.md)). Change a child only through its loaded owner. The save throws when the owner of a changed child is not tracked.
- The read DTO of a root shows `version`. A PUT body that replaces the fields of an edit form has a required `version`, and the entity method calls `GuardVersion` first (see [domain-entities.md](domain-entities.md#versions)).
- `Version` is an opaque number. Compare it only for equality. A list cannot filter or sort on `version`, and `ApplyFilters` and `ApplySortOrder` return 400 for it. They also return 400 for `allowedActions`, because the handler adds it after the page loads. Postgres has no order for `xid`, so a range or a sort fails. An equality filter works in Postgres, but a list has no use for it, so it also gives 400.

Do not catch a `StaleVersionException` of a command that sends a version, and do not send the command again with the new version. The caller did not see the change of the other request, so a retry overwrites it.

## Run again

A command for one intent, such as a new permission, a role change, or a delete, sends no version. The `xmin` check of the owner row still stops its race between the load and the save. But the caller decided nothing on the old rows, so a 409 that says "Reload it" does not help the caller. Mark the command with `IRunsAgainWhenRaced` instead:

```csharp
public sealed record Command(string Id, [property: RequiredField] string Permission)
    : IRequest<UserDto>, IRunsAgainWhenRaced;
```

`RaceRetryBehavior` catches the `StaleVersionException` of a marked command. It calls `AppDbContext.DiscardLostRace`, which clears the tracked entities and the recorded audit actions. Then it waits for a short random time and runs the handler again in the same DI scope. The lost save rolled back, so the database has none of its changes. The caller, the validation, and the request span stay those of the first run. Each lost run that runs again adds a `race.lost` event with `app.race.run` to the request span. After 20 lost runs, the behavior adds a `race.exhausted` event with `app.race.run` and throws a `ConcurrentChangeException`.

Obey these rules for a marked command. A test enforces the first two.

1. The request has no `Version`, also in a DTO in it. With a version, a lost race is a stale version on each run. The behavior then runs the command 20 times, and the caller gets a concurrent change, not the stale version that it must get. `RunAgainRequestTests` fails on such a request.
2. Send the command only from an entrypoint (HTTP, gRPC, or MCP), not from a handler or a domain event handler. `DiscardLostRace` clears each tracked change of the DI scope, also the changes of the handler that sent the command. `EntrypointBoundaryTests` fails when core code creates a marked command.
3. Do the work that leaves the process, such as a log of the change, a metric, or a call to another service, after the save. The handler runs again from the start, so the work before the save runs one time for each run.
4. The handler saves one time. A second run after a lost second save would apply the first save again.
5. The handler gives the same end state when it runs again after its own commit, such as an add of a permission that is already there. In the window of [Lost reply of a commit](#lost-reply-of-a-commit), a save that committed can fail its `xmin` check, and the command runs again. A delete then gets 404 for a delete that worked. Do not mark a command that adds to a value, such as an increment.

The tests do not see these breaks of the rules. Check them in the review:

- A `Version` property with a different name, such as `RowVersion`. `RunAgainRequestTests` looks only for the name `Version`.
- A copy of a marked record with `with` in core code. `EntrypointBoundaryTests` does not count the copy method of a record.
- A background job in the shared plumbing or the composition root that sends a marked command. `EntrypointBoundaryTests` reads only the core types. `DiscardLostRace` then clears the tracked changes of the job.

Each marked command has a race test (see [Race tests](../backend.md#race-tests)). The test pauses the command after its load, commits a rival write to the same aggregate, and checks that both changes saved.

## Lock

A version guards the rows that a command writes. A rule over a set of rows can break with no conflict on any one row. For example, "two open bookings of a room do not overlap", or "a clinic with an open appointment cannot be deactivated". A booking reads the clinic and writes an appointment. A deactivation counts the open appointments and writes the clinic. Each reads the rows that the other writes, so no version catches the race, and a run again does not help.

The template has no such rule yet, so it has no lock class. When a feature needs one, use a Postgres session advisory lock:

1. Give the lock two keys: a number for the kind of the root, and `hashtext` of the ID of the root. The one-key locks of the EF migrations then do not share a key with it.
2. Take the shared lock (`pg_advisory_lock_shared`) in each write that can add to the set, such as a booking. Take the exclusive lock (`pg_advisory_lock`) in each write that needs the set empty, such as a deactivation. Bookings then do not wait on each other, and a deactivation waits for the bookings that started before it.
3. Take the lock before the first read, and free it after the save.
4. A session lock ends when its connection drops. So run the lock, the reads, the checks, and the save in one call of `Database.CreateExecutionStrategy().ExecuteAsync`. A transient failure then runs the whole work again on a new connection, not only the save.
5. Start with an empty change tracker, and throw if it has changes. A retry of the strategy starts with an empty tracker, so it would save without them.
6. If the unlock fails, end the session with `pg_terminate_backend(pg_backend_pid())`. The pool does not reset a connection when it gets it back, so the lock stays on the idle connection until the next request takes it.
7. Write a race test that waits with `PauseAtQuery.WaitUntilBlockedAsync` until the rival waits on the lock.

These options do not fit:

- **A `SERIALIZABLE` transaction around the checks and the save.** One save is one transaction ([ADR 0010](../adr/0010-domain-events-in-the-save-transaction.md)), and `AppDbContext` refuses a save inside a transaction that the handler opens.
- **`SELECT ... FOR SHARE` on the root row.** A row lock ends with its transaction, so it needs the same transaction around the checks and the save.
- **A write to the shared root row in each write that adds to the set, so that its version catches the race.** Each booking of a clinic then conflicts with each other booking of that clinic, and the caller gets a 409 for a booking that had no problem.

## Lost reply of a commit

After a lost acknowledgment of a commit, the execution strategy of EF sends the save again. Before it does, the save looks for its audit entries. If they are there, the save committed, so it returns success and sets the `Version` of each saved entity. Thus the save does not run two times, and the caller does not get a 409 for a save that worked. A save that writes no audit entry, such as a save of only a `[NotAudited]` table, has nothing to look for, and it can still get a 409. See [ADR 0010](../adr/0010-domain-events-in-the-save-transaction.md).
