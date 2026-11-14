# Background jobs run on BackWave and commit with the save

Work outside the request path is a BackWave job. A feature or a domain event handler calls `dbContext.QueueJob(job)`, and the next `SaveChangesAsync` writes the job in the transaction of the save. The job and the change commit together, or neither commits. BackWave keeps its tables in the `backwave` schema of `appdb`, and `MigrationHostedService` migrates that schema after the EF migration.

`JobCommitHook` writes the jobs in `TransactionCommittingAsync` of the transaction that EF begins for the save. No code opens a transaction, so the `SavedChanges` interceptors still run after the commit. A save with jobs and no audit entries also gets a commit check, so a lost reply of the commit writes each job one time.

Each job names its tenant (`ITenantJob`). `QueueJob` refuses a job of a tenant that the caller cannot see. `JobHandler<TJob>` grants the scope of each attempt the caller `TenantJob` of that tenant before the first query. The job then sees the rows of its tenant only, holds each permission in that tenant, and cannot manage all tenants. Each save of the job has the job as its audit request.

The Server runs one worker group for each queue of the jobs. A slow queue, such as the object deletes when storage is down, does not hold up the jobs of another queue.

The object deletes are the first jobs. The save that deletes an attachment queues `DeleteObject`, and the save that deletes a tenant queues `DeleteTenantObjects`. Before this decision, the save added an `ObjectDeleteRequest` row, and a hand-built hosted service claimed the rows, retried them, and marked them dead.

## Considered options

- **Keep a request entity and a hand-built sender for each effect.** ADR 0010 chose this shape for the first effect outside the database. We did not keep it. Each effect needs its own table, claim query, retry, dead state, trace link, and runbook in SQL. A person can see the state only with SQL. BackWave gives the lease, the retry, the dead letter, the trace link, the metrics, and a dashboard for each job.
- **The enqueue of `BackWave.EntityFrameworkCore`.** We did not choose this option. It needs `Database.CurrentTransaction`, and the save refuses an outer transaction (domain-events rule 13).
- **An explicit transaction around the save, and the enqueue before the commit.** We did not choose this option. Each `SavedChangesAsync` then runs before the commit. `TenantLookupInterceptor` would remove the cached lookups too early, and a read between the removal and the commit would cache the old value again. The seam of the lost-reply tests would also throw before the commit, so those tests would test nothing.
- **`AutoTransactionBehavior.Always` for each save.** We did not choose this option. A save with one statement and no job then pays for a transaction. The context sets it only for a save with jobs.
- **The request row as the source of truth, and BackWave only as the runner.** We did not choose this option. One job would then have two state machines that can disagree.
- **Run the job as the user who queued it.** We did not choose this option. The user can lose access, or be deleted, before the job runs.
- **Run the job in a system scope narrowed to one tenant.** We did not choose this option. The permission guard would see a caller that manages all tenants, so a job could pass a check for all tenants. `TenantJob` puts the limit in the identity, where each reader finds it.
- **One payload with a prefix flag for the object deletes.** We did not choose this option. A bad key with the flag set could delete a whole prefix. `DeleteTenantObjects` builds its prefix from the tenant ID only, and `DeleteObject` refuses a key that is not under the prefix of its tenant.
- **A hand-kept list of worker groups.** We did not choose this option. A job on a new queue would run nowhere. The groups come from the queues of the jobs.

## Consequences

- A job runs at least one time. A handler must be safe to run two times, because a lost lease or an operator requeue runs it again. A delete of an object that is gone succeeds.
- A handler failure throws. BackWave records the exception on the process span, counts it in `backwave.jobs.failed`, runs the job again after the delay of its worker group, and dead-letters it after the last attempt. The handler does not log the failure itself.
- A stored payload that does not decode, or that a constructor check refuses, goes to the quarantine, and no handler runs. A delete job with a key of another tenant is such a payload.
- A job keeps the trace of the save that queued it. BackWave stores the `traceparent`, and the process span of each attempt links to the send span of the save. A job needs no `TraceContext`.
- The migration `DropObjectDeleteRequests` refuses to drop `object_delete_requests` when it has rows. Run the previous version until it drains the table, and handle its dead rows. A silent drop would leave objects in storage that nothing deletes.
- A dead object delete is a dead job in BackWave. A person runs it again with a requeue, not with SQL on a table.
