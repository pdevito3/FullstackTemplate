# Audit log

The audit log records each data change and each action that a handler records. `AppDbContext` writes it in the transaction of each save, so a change and its audit entries commit together, or neither commits. The table is `audit_entries`, and it is append-only. [ADR 0012](../adr/0012-audit-log.md) gives the reasons for this design.

## What a save records

A save adds one audit entry for each entity that it adds, changes, or deletes, and one entry for each recorded action. All entries of one save have the same record id.

| Kind | The save | `data` holds |
|------|----------|--------------|
| `Created` | adds the entity | Each value that is not null |
| `Updated` | changes a value | The old and the new value of each changed value |
| `Deleted` | deletes the entity (a soft delete) | The old value of each value that the row held, and the new value of each value that also changed |
| `Restored` | sets `IsDeleted` back to false | The old and the new value of each value that also changed |
| `Action` | saves a recorded action | Each property of the action, except its subject |

The entries do not record the key or the `BaseEntity` bookkeeping: `LastModifiedOn`, `IsDeleted`, and `Version`. An update that changes only these values writes no entry. Thus the owner touch of [Concurrency](../backend.md#concurrency) writes no entry.

Each entry repeats the facts of its save:

- `occurred_at` is the one clock reading of the save. The `LastModifiedOn` of the entities has the same value.
- `actor_subject` is the `sub` of the token, and `actor_client` is its client ID. Both are null for system work. A provider can make the `sub` an email address, so the read shows it only with `read_audit_log_sensitive`.
- `actor_user_id` is the user record of the caller, or of the user that a delegated agent acts for. It is null for system work, for a client that acts for no user, and for a user with no user record yet. `actor_agent_id` is the agent record of a delegated agent or a machine agent.
- `request_name` is the MediatR request, for example `UpdateUser.Command`. A nested request keeps the name of the outer request. The name is null for a save outside a request.
- `trace_id` joins the save to its trace in the Aspire dashboard.

Each entry names its entity (`entity_type`, `entity_id`), its aggregate root (`root_type`, `root_id`), and the tenant of the root. The root is the owner at the end of the `Cascade` foreign keys. Thus the history of a user shows the changes of its `UserPermission` rows. The tenant comes from the root, not from the caller, so the change of a domain event handler in another tenant goes to that tenant.

The key is `(occurred_at, record_id, ordinal)`. One index serves the history of a root, and one serves the list of a tenant, each newest first. The table has no foreign keys, because an entry outlives its entity. Triggers refuse `UPDATE`, `DELETE`, and `TRUNCATE`.

## Rules

1. Write an audited table only through `SaveChangesAsync` or `BulkInsertAsync`. For a change of many rows, load the entities, change them, and save. See [Writes that skip the save](#writes-that-skip-the-save).
2. Give each property of an entity `[Sensitive]` or `[NotSensitive]`. Only a plain value (a `bool`, an enum, a `Guid`, or an integer) and a foreign key need no mark. Text, a date, a time, a `decimal`, a `double`, and a value object need a mark, because each can hold personal data, such as a birth date or a salary. The C# type of the property decides, not the column type. Thus a `decimal` that a value converter stores as a number needs a mark, and an enum that it stores as text needs none. `AuditSensitivityTests` fails on a value with no mark or with both marks.
3. Use `[Sensitive]` for personal or secret data, such as a name, an email address, or free text. Use `[NotSensitive]` for an ID or a name from a fixed set.
4. Map each value object with `ComplexProperty`, not `OwnsOne`. A complex property can hold only simple properties, not a collection and not another complex property.
5. Give each aggregate root a tenant of its own. A root implements `ITenantable`, or it is `Tenant`. A child gets its owner through one `Cascade` foreign key, and it does not implement `ITenantable`. See [multi-tenancy.md](../multi-tenancy.md#make-a-child-entity-multi-tenant).
6. Give each entity class a name that no other entity class has. An entry stores the class name.
7. Make each entity a `BaseEntity`. An entity type that is not a `BaseEntity` has no audit. It must have `[NotAudited(reason)]` with the reason that its rows need no audit, for example `UsedDPoPProof`.
8. Give each action a name with `[AuditActionName("snake_case")]`. Do not change the name after a release. A stored entry finds its action type by this name.
9. After a handler records an action, call `SaveChangesAsync`, also when the handler changes no data.

`AuditModelTests` enforces rules 4 to 7 on the model of `AppDbContext`.

## Record an action

An action is a fact that is not a data change, for example "users exported". Declare a `sealed record` that derives from `AuditAction` in `Domain/[EntityName]s/`. Its subject is the aggregate root that the action is about. Mark each property of the action that is not plain, as for an entity.

```csharp
[AuditActionName("users_exported")]
public sealed record UsersExported(AuditSubject Subject, ExportFormat Format, int Count) : AuditAction(Subject);
```

In the handler, make the subject from the loaded root, record the action, and save:

```csharp
var tenant = await dbContext.Tenants.SingleAsync(t => t.Id == request.TenantId, cancellationToken);
dbContext.RecordAction(new UsersExported(AuditSubject.Of(tenant), request.Format, users.Count));
await dbContext.SaveChangesAsync(cancellationToken);
```

`AuditSubject.Of` takes a `Tenant` or an `ITenantable` root, so the type, the ID, and the tenant come from the entity. The save writes the action after the data changes of the same save, under the same record id. An enum value is stored by its name.

## Bulk insert

`AppDbContext.BulkInsertAsync(roots)` inserts many new aggregates with Postgres COPY, for an import. Use it when a save of the same rows is too slow. It is a save with another writer:

- It sets `LastModifiedOn` and writes a `Created` entry for each row, the children of the roots too.
- It writes the recorded actions of the context.
- It writes all rows in one transaction, and it raises the save events of EF, so each `SaveChangesInterceptor` runs. Thus `TenantLookupInterceptor` forgets a cached miss of each new user.
- After a lost acknowledgment of the commit, it looks for its record id in `audit_entries`. If the rows are there, it returns success.
- After it, the entities are tracked and `Unchanged`, with their `Version`. A later save can change them.
- It returns the number of entity rows that it wrote, the children too.

It refuses these cases with an `InvalidOperationException`:

- The context has pending changes. Save them first.
- The type is a child of an owner. Add the child to its owner, then bulk insert the owner.
- A queued domain event has a handler. The bulk insert runs no handler, so save these entities with `SaveChangesAsync`. It drops an event that has no handler, as a save does.

A unique violation gives a `ConstraintConflictException`, as in a save. A failed bulk insert ends the context, as a failed save does. The bulk insert keeps each row in the change tracker, so split a very large import into parts.

## Writes that skip the save

`ExecuteUpdate`, `ExecuteDelete`, raw SQL, a command on the connection of the context or on a new Npgsql connection, and COPY write no audit entry. Two checks catch them:

- **The build.** `FullstackTemplate.Server/BannedSymbols.txt` bans these APIs in the Server (RS0030): `Database.GetDbConnection()`, `IRelationalConnection.DbConnection`, the `NpgsqlConnection` constructors, and the methods that make an `NpgsqlDataSource` or get a connection from one. The error names the fix.
- **The run time.** `WriteGuard` checks each command that EF does not make itself. It refuses a DO block, a CALL, and dollar-quoted text, because their writes are not in the words of the SQL. It refuses a command that can write and that names a guarded table. The guarded tables are each table of the model, `audit_entries` too, except the tables of a `[NotAudited]` type. A LINQ query, a save, a migration, a value generator, and scaffolding run. A raw query with `FromSql` runs when it does not write. While domain event handlers run, only LINQ queries run (see [domain-events.md](domain-events.md#errors)).

The checks catch mistakes, not code that wants to skip them. The guard does not see the writes of a function that a `SELECT` calls. Reflection or a suppressed ban gets past the build.

To write a `[NotAudited]` table with a banned API, suppress RS0030 on the method with a justification. `BannedApiSuppressionTests` pins the files that suppress the ban, so add the file to its list. The DPoP replay cache is the example.

The migration helpers `GrantPermissionToRole` and `DeletePermission` write `user_permissions` with SQL and no audit entry. The migration history is their record.

## Read the audit log

`GET /api/v1/audit-entries` (`GetAuditEntryPage`) gets one page of entries, newest first.

- With `entityId`, it gets the history of that aggregate root and its children. With no `entityId`, it gets each entry that the tenant filter shows to the caller.
- It needs `read_audit_log`. A caller without it gets 403.
- It shows a value only to a caller with `read_audit_log_sensitive`, or when the value is `[NotSensitive]`, plain, or a foreign key. Otherwise the value has `hidden` set and no old or new value.
- It also hides a value that the code no longer knows, such as the value of a removed property or of a removed action.
- The names, the IDs, the client and the user and agent IDs of the actor, the request name, and the trace id always show. The `sub` of the actor (`actorSubject`) shows only to a caller with `read_audit_log_sensitive`.
- It pages with a cursor, not a page number. Send the `nextCursor` of a page as the `cursor` of the next request. The last page has no `nextCursor`. The response has no total count, because the table only grows.

The cursor text is `{occurred_at in microseconds since 1970}_{record_id}_{ordinal}`. A caller sends it back as it got it. A malformed cursor gives 400.

## Errors

| Cause | Exception |
|-------|-----------|
| `ExecuteUpdate`, `ExecuteDelete`, or raw SQL writes an audited table | `InvalidOperationException` |
| Code adds, changes, or removes an `AuditEntry` | `InvalidOperationException` |
| A request records an action and does not save | `InvalidOperationException` after the handler |
| An action type has no `[AuditActionName]`, or shares a name with another | `InvalidOperationException` |
| A complex property is a collection or holds a complex property | `NotSupportedException` |
| A child has more than one `Cascade` foreign key to an entity | `NotSupportedException` |
| A bulk insert refusal | `InvalidOperationException`. See [Bulk insert](#bulk-insert). |

## Tests

- The unit tests check the model rules (`AuditModelTests`), the marks (`AuditSensitivityTests`), the diff (`AuditDiffTests`), the stored format (`AuditValuesTests`), the redaction (`AuditRedactionTests`), and the guard (`WriteGuardTests`).
- To test the entries of a feature, send its command, then read `AuditEntries` with `ReadAsync` in an integration test. `AuditCaptureTests` and `AuditActionTests` are the examples.
- `BulkInsertTests` proves the bulk insert against Postgres, also a lost commit and a conflict.
- `GetAuditEntryPageTests` proves the endpoint and the redaction for each permission.

## Telemetry

The read tags its feature span with `app.audit.entity_id` and `app.audit.reads_sensitive`. A bulk insert has a `database.bulk_insert` span with `app.bulk_insert.entity_type` and `app.bulk_insert.row_count`. Npgsql adds a span for each COPY. See [feature-telemetry.md](feature-telemetry.md).
