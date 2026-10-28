---
name: audit-log
description: Keeps the audit log complete and its personal data hidden. Use when you add or change an entity, a property, a value object, or an entity configuration; when a handler must record a fact that changes no data, such as an export; when you need a set-based write, raw SQL, or an import of many rows; when RS0030, WriteGuard, AuditModelTests, or AuditSensitivityTests fails; or when you read the history of an entity.
---

# Audit log

Each save writes the audit log in its transaction, so a feature gets its audit entries with no audit code. A change keeps the log complete and its personal data hidden only when it obeys the rules in `docs/backend/audit-log.md`. Read the doc before you start. The reasons are in `docs/adr/0012-audit-log.md`.

## Procedure

1. Find each case of your change, and do its steps.
   - **A new entity, property, or value object.** Mark each property `[Sensitive]` or `[NotSensitive]`, except a flag, an enum, a `Guid`, an integer, or a foreign key. A date and a decimal need a mark. Personal and secret data is `[Sensitive]`. Make a root `ITenantable`, or make the entity a child with one `Cascade` foreign key to its owner. Map a value object with `ComplexProperty`.
   - **A fact that changes no data**, such as an export. Declare a sealed `AuditAction` record with an `[AuditActionName]`, and mark each property that is not a flag, an enum, a `Guid`, or an integer. In the handler, call `dbContext.RecordAction` with `AuditSubject.Of(root)`, then `SaveChangesAsync`.
   - **A change of many rows.** Load the entities, change them through their methods, and save. For many new aggregates, call `dbContext.BulkInsertAsync`.
   - **A table that needs no audit**, such as a cache. Make the type a class that is not a `BaseEntity`, and give it `[NotAudited(reason)]`.
2. Build the Server. An RS0030 error for a write names a write that skips the save. Change the write to a save. Do not suppress the error for an audited table. For an RS0030 error on `Like` or `ILike`, see `docs/backend/features-and-cqrs.md`, section "Search text".
3. Run the unit tests. `AuditModelTests` and `AuditSensitivityTests` fail on a model rule or a mark that is missing, and the message names the fix.
4. Write an integration test that sends the command, then reads `AuditEntries` with `ReadAsync`. Assert the kind, the values, the actor, and the request name. `AuditCaptureTests` and `AuditActionTests` are the examples.
5. In the running stack, do the change, then get `/api/v1/audit-entries?entityId={root id}` through the BFF. Make sure that the entry has the request name and the trace id of your request.

## Review the result

Before you call the change done, make sure that each item is true:

- [ ] Each new value of an entity or an action that is not a flag, an enum, a `Guid`, an integer, or a foreign key has one mark, and each personal value is `[Sensitive]`.
- [ ] Each write of an audited table goes through `SaveChangesAsync` or `BulkInsertAsync`.
- [ ] Each new RS0030 suppression of a write is for a `[NotAudited]` table, has a justification, and is on the list of `BannedApiSuppressionTests`. For a regex or reflection API, see the `source-generators` skill.
- [ ] Each action has an `[AuditActionName]` that no other action has. The name of a released action is the same as before.
- [ ] Each success path of a handler that records an action calls `SaveChangesAsync`.
- [ ] No code or migration adds, changes, or deletes a row of `audit_entries`. The save and the bulk insert write each row.
- [ ] The integration test reads the entries of the change, and the stack shows them through the BFF.
