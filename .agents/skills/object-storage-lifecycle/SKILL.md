---
name: object-storage-lifecycle
description: Model-checks the life of an attachment row, its object in the bucket, the BackWave jobs that delete objects, and the sweep of the objects that no row names, with TLA+ and TLC. Use when you change AttachmentStorage, an add or delete of an attachment, DeleteTenant, RootLock, AppDbContext.PrepareWrite, ObjectDeletes, the DeleteObject, DeleteTenantObjects, or SweepObjects job, ObjectDeleteOptions, ObjectSweepOptions, AttachmentOptions, or ObjectStorage, or when you ask whether an object can outlive its row or a row can lose its object.
---

# Object storage lifecycle

A TLA+ model of the writes that keep an attachment row and its object in step, checked with TLC over every order of steps in a small scope. It covers crashes and restarts between any two steps, lost commit replies, a canceled upload, a lease that runs out while its worker still works, failed storage calls, a prefix delete that races an add, and a sweep that races an add.

## The rules

Safety. The harm is a file in the list that no one can download.

- `NoLiveRowWithoutObject`. A live row of a live tenant always has its object in the bucket.
- `NoLiveRowInDeletedTenant`. After a tenant delete commits, no row of the tenant is live, so the prefix delete cannot take the object of a live row.

Liveness. Each state that is not an end state has a way out.

- `RequestsSettle`. Each object delete job succeeds, or it is dead.
- `AddsSettle`. Each add returns, throws, or dies with its process. It never waits forever.
- `CommitsSettle` and `TenantDeleteSettles`. A COMMIT in flight ends, and a tenant delete that holds the exclusive lock ends.

Coverage. The harm is storage that is paid for and that no code will ever delete.

- `OrphansGo`. Each object that no live row names, and that no add can still save a row for, is deleted. This is a liveness rule: it holds when the schedule starts the sweep again and a storage that fails works again at some time.

A witness, which is not a rule:

- `NoObjectLeftInDeletedTenant`. It breaks when an object stays under a deleted tenant after its `DeleteTenantObjects` job succeeded, so TLC shows that path. `DeletedTenantOrphan.cfg` checks `OrphansGo` on the same scope, which shows that the sweep deletes that object.

## Run

```sh
sh .agents/skills/object-storage-lifecycle/check.sh
```

The first run installs the pinned TLC (tla2tools 1.7.4) and Java (temurin-25) with mise from `model/mise.toml`, so it needs the network once. The run takes about 1.5 minutes on an idle laptop. `check.sh Liveness LateCommit` runs only the configs that you name. Each line says `baseline` when the config gives its expected result, and `CHANGED` with the folder of the TLC output when it does not. The exit code is 0 only when every config is at its baseline.

| Config | Scope | Expected |
|---|---|---|
| `ObjectStorage.cfg` | 2 uploads, 2 workers, 1 attempt, 1 crash, late commits, the sweep. About 900,000 states | pass |
| `Liveness.cfg` | 1 upload, 2 workers, 2 attempts, 1 crash, late commits, the sweep | pass |
| `Chart.cfg` | 1 upload, 1 worker, 1 attempt, no crash. Small enough for a chart | pass |
| `DeletedTenantOrphan.cfg` | 1 upload, 1 crash. The witness and `OrphansGo` | `NoObjectLeftInDeletedTenant` breaks, `OrphansGo` holds |
| `LateCommit.cfg` | The cleanup at once of a failed add (`Cleanup = TRUE`, `DeleteObjectWithNoRowAsync`), and late commits | `NoLiveRowWithoutObject` breaks (F2) |
| `GraceNone.cfg` | A grace shorter than the time limit of an add | `NoLiveRowWithoutObject` breaks |
| `GraceLimit.cfg` | A grace of the time limit with no margin, and late commits | `NoLiveRowWithoutObject` breaks |
| `SoftDeletedNames.cfg` | A sweep that keeps the object of a soft-deleted row | `OrphansGo` breaks |

Read each result with the pstack `/statechart` skill, with `model/ObjectStorage.tla`, the config, and `model/view.mjs` as the view. For a rule that breaks, its report gives the shortest path in the words of a user. For `Chart.cfg`, it draws how one attachment moves between states.

## Does your change touch the verified code

The coverage record. The sync commit is `80d9f8f4`: at that commit the model with `Cleanup = TRUE` describes this code. The sweep (`Age`, `SweepList`, `SweepCheck`, `SweepDelete`, `SweepFail`) and `Cleanup = FALSE` are the design of the change that follows. The code does not have them yet.

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
- The prefix delete and the sweep list one page. A list that a later page gives is the same race as a list that runs later.
- `Grace = "add"` stands for a grace of `AttachmentOptions.AddTimeout` plus `ObjectSweepOptions.Margin`. It needs these facts: S3 sets `LastModified` at or after the start of the put, `AddAsync` stops the put and the save at `AddTimeout`, the clocks of the Server and of S3 differ by at most 15 minutes (SigV4 refuses a request with more skew), and a COMMIT that is in flight at the cancel ends within the rest of the margin. A COMMIT that stays in flight longer, for example on a synchronous standby that is down while the cancel cannot reach Postgres, breaks the last fact. `GraceLimit.cfg` shows the harm.
- The sweep reads `now` before it reads the rows, so an object is old before the row check. The model decides the age at `SweepList`.
- One sweep runs at a time (the schedule skips a tick while a run runs). Two sweeps that overlap each make a safe decision alone, so one sweep shows each race. A process death of the sweep is `SweepFail` with no key deleted.
- No code restores a soft-deleted row, so a sweep that deletes the object of a soft-deleted row takes no file from the list.
- A retry of the execution strategy in `RootLock.RunAsync` ends in one of the outcomes of `CommitRow`, so the model does not show it as its own steps.
- Out of scope: a PUT to S3 that lands after the client gave up on it (an object with no row, as in F1, which the sweep deletes as any other), the presigned URLs, a dead job that a person runs again from the jobs dashboard, the frontend, and more than one tenant.
- A pass covers the configs in the table, not all sizes.
- The check does not catch a removed lease fence of BackWave (`Fenced`). With it gone, every config stays at its baseline. A stale outcome only picks which of two attempts sets the state of the job, and both outcomes keep each rule here, because a delete of a gone object succeeds and a key is never used again.

## Proof that the check can fail

Each break below was made in the real code and in the model, and then both were reverted, and `check.sh` was back at its baseline.

- `DeleteObjectWithNoRowAsync` with no `if (saved)` return, so the cleanup deletes the object of a row that committed. `ObjectStorage.cfg` breaks `NoLiveRowWithoutObject`: the put returns, the save takes the tenant lock, the save checks the workshop, the save commits and throws, the cleanup checks for the row, and the cleanup deletes the object of a file that is in the list.
- `AddWorkshopAttachment` with no `RootLock.Tenant.RunSharedAsync` around the save. `ObjectStorage.cfg` breaks `NoLiveRowInDeletedTenant`: the save checks the workshop, a tenant delete takes the lock and commits, and then the save commits a live row in the deleted tenant, whose object the prefix delete then takes.
- A sweep with no row check, in the model only. `Chart.cfg` and `ObjectStorage.cfg` break `NoLiveRowWithoutObject`: the put returns, the save commits, the grace passes, the sweep lists the object, and it deletes the object of a file that is in the list.
- A sweep that reads `now` after the row check, in the model only. `Chart.cfg` breaks `NoLiveRowWithoutObject`: the sweep lists the object while the save still runs, reads no row, the save commits, the grace passes, and the sweep deletes the object.
- A sweep that can fail forever, with no strong fairness on `SweepDelete`, in the model only. `Liveness.cfg` and `DeletedTenantOrphan.cfg` break `OrphansGo`.
- A claim that adds no attempt, in the model only. `Liveness.cfg` breaks `RequestsSettle`: a worker claims the `DeleteTenantObjects` job, its storage call fails, and the job is due again with the same count, so it never goes dead.

A run that ends in `error`, with a TLC log that stops with no TLC error and no `TLC exit code` line, means that the JVM was stopped from outside, for example by memory pressure. Run the config again.

## Findings and decisions

F1. An object stayed in the bucket with no row and no job, and no code deleted it. The causes: a process that dies after `PutAsync` and before the save or the cleanup ends, a cleanup whose row check or delete fails, and, as the worst case, a put that lands under a deleted tenant after its `DeleteTenantObjects` job listed the prefix. The sweep fixes F1: `OrphansGo` holds in `Liveness.cfg` and in `DeletedTenantOrphan.cfg`. In `DeletedTenantOrphan.cfg`, the witness gives the worst case: a user starts an upload, the tenant delete commits, the job lists the prefix, the put lands and throws, and the job succeeds.

F2. A live row could lose its object (`LateCommit.cfg`). The save of the row throws while its COMMIT is still in flight on the backend, for example after a user cancels the upload and Npgsql gives up on the cancel of a slow COMMIT (synchronous replication, a slow fsync). The cleanup at once checks for the row on a new connection and does not see it, the COMMIT then lands, and the cleanup deletes the object. The list then shows a file that no one can download. With the sweep and no cleanup at once (`Cleanup = FALSE`), `ObjectStorage.cfg` passes with late commits on, so the sweep removes F2. It is found in the model, not reproduced in the real app.

The decisions of the sweep, each with its evidence:

- The grace is `AttachmentOptions.AddTimeout` plus `ObjectSweepOptions.Margin`. A grace shorter than the time limit (`GraceNone.cfg`) deletes the object of an add that still saves its row. A grace of the time limit with no margin (`GraceLimit.cfg`) deletes the object while a COMMIT is in flight, and the COMMIT then lands.
- A soft-deleted row does not name its key. With it (`SoftDeletedNames.cfg`), the object of a deleted file whose delete job is dead stays, and so does the object of a deleted tenant.
- The add has no cleanup at once. The cleanup is the cause of F2, and the sweep deletes each object that it deleted.
