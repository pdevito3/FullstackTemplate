---
name: object-storage-lifecycle
description: Model-checks the life of an attachment row, its object in the bucket, and the BackWave jobs that delete objects, with TLA+ and TLC. Use when you change AttachmentStorage, an add or delete of an attachment, DeleteTenant, RootLock, AppDbContext.PrepareWrite, ObjectDeletes, the DeleteObject or DeleteTenantObjects job, ObjectDeleteOptions, or ObjectStorage, or when you ask whether an object can outlive its row or a row can lose its object.
---

# Object storage lifecycle

A TLA+ model of the writes that keep an attachment row and its object in step, checked with TLC over every order of steps in a small scope. It covers crashes and restarts between any two steps, lost commit replies, a canceled upload, a lease that runs out while its worker still works, failed storage calls, and a prefix delete that races an add.

## The rules

Safety. The harm is a file in the list that no one can download.

- `NoLiveRowWithoutObject`. A live row of a live tenant always has its object in the bucket.
- `NoLiveRowInDeletedTenant`. After a tenant delete commits, no row of the tenant is live, so the prefix delete cannot take the object of a live row.

Liveness. Each state that is not an end state has a way out.

- `RequestsSettle`. Each object delete job succeeds, or it is dead.
- `AddsSettle`. Each add returns, throws, or dies with its process. It never waits forever.
- `CommitsSettle` and `TenantDeleteSettles`. A COMMIT in flight ends, and a tenant delete that holds the exclusive lock ends.

Coverage. The harm is storage that is paid for and that no code will ever delete. These rules break today (finding F1).

- `EveryObjectAccounted`. Each object has a live row, or an add that still owns it, or a job that will delete it, or a dead job that a person can see.
- `NoObjectLeftInDeletedTenant`. After the `DeleteTenantObjects` job of a deleted tenant succeeds, no object of the tenant stays, except one that an add still owns.

## Run

```sh
sh .agents/skills/object-storage-lifecycle/check.sh
```

The first run installs the pinned TLC (tla2tools 1.7.4) and Java (temurin-25) with mise from `model/mise.toml`, so it needs the network once. The run takes about 2 minutes on an idle laptop. `check.sh Liveness LateCommit` runs only the configs that you name. Each line says `baseline` when the config gives its expected result, and `CHANGED` with the folder of the TLC output when it does not. The exit code is 0 only when every config is at its baseline.

| Config | Scope | Expected |
|---|---|---|
| `ObjectStorage.cfg` | 2 uploads, 2 workers, 1 attempt, 1 crash, cleanup failures. About 130,000 states | pass |
| `Liveness.cfg` | 1 upload, 2 workers, 2 attempts, 1 crash, cleanup failures, late commits | pass |
| `Chart.cfg` | 1 upload, 1 worker, 1 attempt, no crash. Small enough for a chart | pass |
| `OrphansCrashOnly.cfg` | 1 upload, 1 crash, cleanup always works | `EveryObjectAccounted`, `NoObjectLeftInDeletedTenant` break (F1) |
| `OrphansNoCrash.cfg` | 1 upload, no crash, cleanup failures | `EveryObjectAccounted`, `NoObjectLeftInDeletedTenant` break (F1) |
| `LateCommit.cfg` | 1 upload, a save that throws while its COMMIT is in flight | `NoLiveRowWithoutObject` breaks (F2) |

Read each result with the pstack `/statechart` skill, with `model/ObjectStorage.tla`, the config, and `model/view.mjs` as the view. For a rule that breaks, its report gives the shortest path in the words of a user. For `Chart.cfg`, it draws how one attachment moves between states.

## Does your change touch the verified code

The coverage record. The sync commit is `80d9f8f4`: at that commit the model describes this code.

| Model | Code |
|---|---|
| `StartAdd`, `Put`, `StopAdd`, `CleanupCheck`, `CleanupDelete` | `FullstackTemplate.Server/Domain/Attachments/AttachmentStorage.cs`: `AddAsync`, `DeleteObjectWithNoRowAsync` |
| `TakeShared`, `CheckWorkshop`, `CommitRow` | `Examples/Workshops/Features/AddWorkshopAttachment/AddWorkshopAttachment.cs`: `Handle` and its `saveRow`. `Databases/RootLock.cs`: `RunSharedAsync`, `RunAsync` |
| `DeleteAttachment` | `AttachmentStorage.DeleteAsync`, `Examples/Workshops/Features/DeleteWorkshopAttachment/DeleteWorkshopAttachment.cs` |
| `TakeExclusive`, `CommitTenantDelete` | `Domain/Tenants/Features/DeleteTenant/DeleteTenant.cs`: `Handle`. `RootLock.RunExclusiveAsync` |
| `NewRequest`, `Covers` | `Databases/AppDbContext.cs`: `PrepareWrite`. `Domain/ObjectDeletes/ObjectDeletes.cs`: `ForDeleted` |
| `Claim`, `LeaseExpires`, `Fenced`, `CrashRunner` | The BackWave worker group of the `object-deletes` queue: its claim, lease, heartbeat, and outcome fence. `Domain/ObjectDeletes/ObjectDeleteOptions.cs`: `WorkerGroup` |
| `Work`, `DeletePage` | `Domain/ObjectDeletes/DeleteObject.cs`: `DeleteObjectHandler`. `DeleteTenantObjects.cs`: `DeleteTenantObjectsHandler`. `ObjectDeletes.InAttemptTimeAsync`. `Resources/Storage/ObjectStorage.cs`: `DeleteAsync`, `DeleteUnderAsync` |
| `Complete`, `Fail` | `Resources/Jobs/JobHandler.cs`. The `RetryPolicy` of `ObjectDeleteOptions.WorkerGroup`, which dead-letters a job at `MaxAttempts` |

Paths in the table are under `FullstackTemplate.Server/`. A new attachment entity with its own add or delete handler is covered when its handler has the same shape as the workshop handlers. If it does not, model it.

To see what changed in the covered code since the sync commit:

```sh
git log --oneline 80d9f8f4..HEAD -- FullstackTemplate.Server/Domain/Attachments FullstackTemplate.Server/Domain/ObjectDeletes FullstackTemplate.Server/Databases/RootLock.cs FullstackTemplate.Server/Databases/AppDbContext.cs FullstackTemplate.Server/Domain/Tenants/Features/DeleteTenant FullstackTemplate.Server/Resources/Storage FullstackTemplate.Server/Resources/Jobs FullstackTemplate.Server/Examples/Workshops/Features Directory.Packages.props
```

When an intended change moves a step, change the action that names that code in `model/ObjectStorage.tla`, run `check.sh`, and change the expected results in `check.sh` only for a finding that the change fixes or adds. Move the sync commit forward only after you confirm, function by function, that the model describes the code at the new commit.

## A model error or a code bug

When a config leaves its baseline, get the path from `/statechart`, then replay it in the code, one action at a time.

- Each action names the code it stands for. If one step of the path is a step that the code cannot take, for example a claim of a job whose due time has not passed, or a commit that a lock should keep out, the model is wrong. Correct the model, not the rule.
- If each step is a step that the code can take, it is a code bug. Report it with the path in the words of the code. Do not weaken a rule or remove a fault until the check passes.
- TLC errors that are not a broken rule (a parse error, `error` in the result) are model errors.
- A finding that disappears (`CHANGED`, with fewer broken rules) means that a change fixed it, or that the model lost a fault. Find which, then update the expected results and the findings below.

## Assumptions and scope

- One tenant. Each upload has its own key, so a key is never used two times. The `DeleteObject` constructor refuses a key of another tenant, so one tenant shows each race.
- The delay of a retry only delays a claim, so the model lets a due job be claimed at any time. A lease can run out at any time, which covers a worker slower than its lease and a heartbeat that stops. BackWave counts the expired claim as an attempt.
- A shutdown that gives a job back with no failed attempt is a crash in the model, which counts the attempt. That only makes a job dead sooner in the model.
- The prefix delete lists one page. A list that a later page gives is the same race as a list that runs later.
- A retry of the execution strategy in `RootLock.RunAsync` ends in one of the outcomes of `CommitRow`, so the model does not show it as its own steps.
- Out of scope: a PUT to S3 that lands after the client gave up on it (the same harm as F1), the presigned URLs, a dead job that a person runs again from the jobs dashboard, the frontend, and more than one tenant.
- A pass covers the configs in the table, not all sizes.
- The check does not catch a removed lease fence of BackWave (`Fenced`). With it gone, every config stays at its baseline. A stale outcome only picks which of two attempts sets the state of the job, and both outcomes keep each rule here, because a delete of a gone object succeeds and a key is never used again.

## Proof that the check can fail

Each break below was made in the real code and in the model, and then both were reverted, and `check.sh` was back at its baseline.

- `DeleteObjectWithNoRowAsync` with no `if (saved)` return, so the cleanup deletes the object of a row that committed. `ObjectStorage.cfg` breaks `NoLiveRowWithoutObject`: the put returns, the save takes the tenant lock, the save checks the workshop, the save commits and throws, the cleanup checks for the row, and the cleanup deletes the object of a file that is in the list.
- `AddWorkshopAttachment` with no `RootLock.Tenant.RunSharedAsync` around the save. `ObjectStorage.cfg` breaks `NoLiveRowInDeletedTenant`: the save checks the workshop, a tenant delete takes the lock and commits, and then the save commits a live row in the deleted tenant, whose object the prefix delete then takes.
- A claim that adds no attempt, in the model only. `Liveness.cfg` breaks `RequestsSettle`: a worker claims the `DeleteTenantObjects` job, its storage call fails, and the job is due again with the same count, so it never goes dead.

A run that ends in `error`, with a TLC log that stops with no TLC error and no `TLC exit code` line, means that the JVM was stopped from outside, for example by memory pressure. Run the config again.

## Findings

F1. An object can stay in the bucket with no row and no job, and no code deletes it. Two causes:

- A crash. `OrphansCrashOnly.cfg`: a user uploads a file, and the process dies after `PutAsync` and before the save or the cleanup ends. The object stays until a tenant delete. The worst case: a user starts an upload, `DeleteTenant` commits, a worker claims the `DeleteTenantObjects` job and lists the prefix while the put is still in flight, the process dies, the put lands, and the job succeeds. The object stays under the prefix of the deleted tenant, and no job covers it.
- A failed cleanup, with no crash. `OrphansNoCrash.cfg`: the put fails after storage kept the bytes (or the save fails), and the `AnyAsync` row check of `DeleteObjectWithNoRowAsync` throws, or its `DeleteAsync` throws. The code logs an error and keeps the object.

A fix needs a durable intent before the put, such as a job for the key that the save of the row deletes, or a sweeper that deletes keys with no row.

F2. A live row can lose its object (`LateCommit.cfg`). The save of the row throws while its COMMIT is still in flight on the backend, for example after a user cancels the upload and Npgsql gives up on the cancel of a slow COMMIT (synchronous replication, a slow fsync). The cleanup checks for the row on a new connection and does not see it, the COMMIT then lands, and the cleanup deletes the object. The list then shows a file that no one can download. This needs a COMMIT that ends after the cleanup check, so it is rare. It is found in the model, not reproduced in the real app.
