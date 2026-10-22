# Use the Postgres xmin column for optimistic concurrency

Each entity has a `Version` property that maps to the Postgres `xmin` system column. `xmin` is the ID of the transaction that wrote the current version of the row, so Postgres changes it on each write of the row. EF Core uses it as a concurrency token: each `UPDATE` has `WHERE xmin = @loaded`. When another save changed the row after the load, no row matches, and the API returns 409.

The rule covers the aggregate, not only the root row. A save that adds, changes, or removes an owned child (the dependent of a `Cascade` foreign key) also updates the owner row, so the owner's `xmin` check guards the whole aggregate. For example, two requests that each reserve the last part of a stock item conflict, also when each reservation is a child row.

The read DTO of an aggregate root shows `version`, and a PUT body that replaces fields from an edit form must send it back. The entity compares the two with `GuardVersion`. Thus a user who saves a stale form gets a 409, and does not overwrite the change of another user.

## Considered options

- **An explicit `version` column that `SaveChanges` increments.** It is portable, and the number means something to a person. We did not choose this option. Each table gets a new column and a migration. A write that does not go through `SaveChanges`, such as `ExecuteUpdateAsync` or raw SQL, does not increment it, and nothing shows the error. Postgres changes `xmin` on every write, from every writer.
- **Concurrency for the root row only.** We did not choose this option. A change to a child row then does not conflict with a concurrent change to the same aggregate, so an invariant that counts the children (stock, bookings, seats) can break.
- **An `IAggregateRoot` marker, or a `Touch()` call in each entity method that changes a child.** We did not choose this option. The configuration already says which child an entity owns (`Cascade`) and which link goes to another aggregate (`Restrict`). A second list can disagree with it, and a new method can forget the call.
- **An `If-Match` header with an ETag.** It is the HTTP standard. We did not choose this option. The core cannot read HTTP headers, gRPC and MCP have no such header, and a generated client makes a required body field part of the type, so a form cannot forget it.
- **A check only in `SaveChanges`, with the client version as the EF original value.** We did not choose this option. It puts EF code in each handler, and the result depends on how EF detects changes. `GuardVersion` keeps the rule in the entity, where a unit test can read it.

## Consequences

- A new entity gets the token from `BaseEntity` with no configuration. The migration that adds `xmin` to the model has no DDL, because `xmin` is a system column.
- `Version` is an opaque 32-bit number. Compare it only for equality. It changes after a dump and restore, so an edit form that is open at that time gets one 409.
- `xmin` is specific to Postgres. The template uses only Postgres. A move to another database needs a version column.
- A change to a child also writes the owner row and its `LastModifiedOn`. Two admins who add different permissions to one user at the same time get one 409. A client loads the record again and repeats the change.
- A child must change through its loaded owner. The save throws when the owner of a changed child is not tracked.
- A stale save that changes no value also gives 409. Thus a retry of a PUT whose response was lost gets 409, and the client loads the record again.
- Unique violations (`23505`) and exclusion violations (`23P01`) map to 409 in one place, `DatabaseConflict.From`, together with the concurrency failure.
- The SPA has no edit form yet, so it has no code for the version. The generated client makes `version` a required field of each PUT body, and [server-state.md](../frontend/server-state.md#forms-with-server-data) gives the rule for the first edit form. The toast already shows the `detail` of a 409.
- gRPC maps each conflict to `AlreadyExists`. No gRPC method updates a record now. When one does, map a stale version to `Aborted`.
