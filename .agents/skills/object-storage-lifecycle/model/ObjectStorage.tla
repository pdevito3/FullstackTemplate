---------------------------- MODULE ObjectStorage ----------------------------
(***************************************************************************)
(* The life of an attachment row, its object in the bucket, the BackWave   *)
(* jobs that delete objects, and the sweep of the objects that no row      *)
(* names, in FullstackTemplate.Server.                                     *)
(*                                                                         *)
(* One tenant. Each attachment in Atts is one upload with its own key, so  *)
(* a key is never used two times. The job ids are the attachments (a       *)
(* DeleteObject job) and "tenant" (the DeleteTenantObjects job).           *)
(*                                                                         *)
(* Each action names the code it stands for. A step of the model is one    *)
(* indivisible step in the code: one S3 call or one Postgres transaction.  *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    Atts,          \* the uploads
    Runners,       \* BackWave workers of the object-deletes queue
    MaxAttempts,   \* ObjectDeleteOptions.MaxAttempts, the RetryPolicy of the worker group
    MaxCrashes,    \* process deaths in one behavior (adder, runner, or tenant delete)
    Cleanup,       \* TRUE: an add that throws deletes its object at once if no row names it (DeleteObjectWithNoRowAsync)
    CleanupFails,  \* the row check or the storage delete of that cleanup can fail
    LateCommit,    \* a save can throw while its COMMIT is still in flight on the backend
    Grace,         \* when an object is old enough for the sweep: "add", "limit", or "none" (see Age)
    SoftDeletedNames \* TRUE: the sweep keeps the object of a soft-deleted row

Tenant == "tenant"
Reqs == Atts \cup {Tenant}
NoReq == "-"

\* The add of one attachment, in AttachmentStorage.AddAsync and the saveRow of AddWorkshopAttachment
AddStates == {"idle",        \* not started
              "put",         \* the workshop loaded (tenant live), PutAsync not yet done
              "lock",        \* waits for RootLock.Tenant.RunSharedAsync
              "check",       \* holds the shared lock, the workshop check is next
              "commit",      \* SaveChangesAsync of the row is next
              "cleanCheck",  \* DeleteObjectWithNoRowAsync: the IgnoreQueryFilters AnyAsync is next
              "cleanDelete", \* DeleteObjectWithNoRowAsync: storage.DeleteAsync is next
              "done",        \* AddAsync returned or threw
              "crashed"}     \* the process died during the add
AddOwns(a, apcv) == apcv[a] \in {"put", "lock", "check", "commit", "cleanCheck", "cleanDelete"}

RunStates == {"idle", "work", "listed", "complete", "fail"}

\* An add that can still make its row live: it has not thrown, or its COMMIT is still in flight.
AddCanSave(a, apcv, rowv) == apcv[a] \in {"put", "lock", "check", "commit"} \/ rowv[a] = "pending"

\* An object that no live row names and that no add can still save a row for.
Orphan(a, objv, rowv, apcv) == objv[a] /\ rowv[a] \notin {"live", "pending"} /\ apcv[a] \in {"done", "crashed"}

VARIABLES
    tenant,   \* "live" or "deleted" (soft delete of the Tenant row)
    row,      \* row[a]: "none", "pending" (COMMIT in flight), "live", "deleted" (soft delete)
    obj,      \* obj[a]: the object of a is in the bucket
    apc,      \* apc[a]: AddStates
    shared,   \* the attachments whose session holds the shared tenant lock
    td,       \* DeleteTenant: "none", "locked" (holds the exclusive lock), "done"
    req,      \* req[q]: the job, [st |-> "none"|"due"|"leased"|"gone"|"dead", att |-> attempts]
    rpc,      \* rpc[r]: RunStates
    rq,       \* rq[r]: the job that r claimed
    ratt,     \* ratt[r]: the attempt count of the claim of r
    rlist,    \* rlist[r]: the keys that ListObjectsV2 gave to r
    old,      \* old[a]: the object of a is older than the grace of the sweep
    spc,      \* the sweep: "idle", "listed" (skeys: the old keys of the page), "checked" (skeys: the keys to delete)
    skeys,
    crashes

vars == <<tenant, row, obj, apc, shared, td, req, rpc, rq, ratt, rlist, old, spc, skeys, crashes>>
addVars == <<row, obj, apc, shared>>
runVars == <<rpc, rq, ratt, rlist>>
sweepVars == <<old, spc, skeys>>

TypeOK ==
    /\ tenant \in {"live", "deleted"}
    /\ row \in [Atts -> {"none", "pending", "live", "deleted"}]
    /\ obj \in [Atts -> BOOLEAN]
    /\ apc \in [Atts -> AddStates]
    /\ shared \subseteq Atts
    /\ td \in {"none", "locked", "done"}
    /\ req \in [Reqs -> [st : {"none", "due", "leased", "gone", "dead"}, att : Nat]]
    /\ rpc \in [Runners -> RunStates]
    /\ rq \in [Runners -> Reqs \cup {NoReq}]
    /\ ratt \in [Runners -> Nat]
    /\ rlist \in [Runners -> SUBSET Atts]
    /\ old \in [Atts -> BOOLEAN]
    /\ spc \in {"idle", "listed", "checked"}
    /\ skeys \subseteq Atts
    /\ crashes \in 0..MaxCrashes

Init ==
    /\ tenant = "live"
    /\ row = [a \in Atts |-> "none"]
    /\ obj = [a \in Atts |-> FALSE]
    /\ apc = [a \in Atts |-> "idle"]
    /\ shared = {}
    /\ td = "none"
    /\ req = [q \in Reqs |-> [st |-> "none", att |-> 0]]
    /\ rpc = [r \in Runners |-> "idle"]
    /\ rq = [r \in Runners |-> NoReq]
    /\ ratt = [r \in Runners |-> 0]
    /\ rlist = [r \in Runners |-> {}]
    /\ old = [a \in Atts |-> FALSE]
    /\ spc = "idle"
    /\ skeys = {}
    /\ crashes = 0

NewRequest == [st |-> "due", att |-> 0]

\* Where an add goes when it throws: the cleanup, or out with its object left for the sweep.
Thrown == IF Cleanup THEN "cleanCheck" ELSE "done"

-----------------------------------------------------------------------------
(* The add. AddWorkshopAttachment.Handle and AttachmentStorage.AddAsync.  *)

\* Handle: the workshop loads through the soft delete filter, so the tenant is live here
StartAdd(a) ==
    /\ apc[a] = "idle" /\ tenant = "live"
    /\ apc' = [apc EXCEPT ![a] = "put"]
    /\ UNCHANGED <<tenant, row, obj, shared, td, req, runVars, sweepVars, crashes>>

\* AddAsync: storage.PutAsync. A put that throws can still have stored the object.
Put(a) ==
    /\ apc[a] = "put"
    /\ \/ /\ obj' = [obj EXCEPT ![a] = TRUE]
          /\ apc' = [apc EXCEPT ![a] = "lock"]
       \/ /\ obj' \in {obj, [obj EXCEPT ![a] = TRUE]}
          /\ apc' = [apc EXCEPT ![a] = Thrown]
    /\ UNCHANGED <<tenant, row, shared, td, req, runVars, sweepVars, crashes>>

\* saveRow: RootLock.Tenant.RunSharedAsync takes pg_advisory_lock_shared. It waits while DeleteTenant holds the exclusive lock.
TakeShared(a) ==
    /\ apc[a] = "lock" /\ td # "locked"
    /\ shared' = shared \cup {a}
    /\ apc' = [apc EXCEPT ![a] = "check"]
    /\ UNCHANGED <<tenant, row, obj, td, req, runVars, sweepVars, crashes>>

\* saveRow: the workshop GetById under the soft delete filter. A deleted tenant gives 404, which throws.
CheckWorkshop(a) ==
    /\ apc[a] = "check"
    /\ IF tenant = "live"
         THEN /\ apc' = [apc EXCEPT ![a] = "commit"]
              /\ UNCHANGED shared
         ELSE /\ apc' = [apc EXCEPT ![a] = Thrown]
              /\ shared' = shared \ {a}
    /\ UNCHANGED <<tenant, row, obj, td, req, runVars, sweepVars, crashes>>

\* saveRow: SaveChangesAsync. It commits and returns, rolls back and throws, commits and throws
\* (a lost reply), or, with LateCommit, throws while the COMMIT is still in flight on its backend.
\* The execution strategy that runs the work again ends in one of these outcomes too.
CommitRow(a) ==
    /\ apc[a] = "commit"
    /\ \/ /\ row' = [row EXCEPT ![a] = "live"]
          /\ shared' = shared \ {a}
          /\ apc' = [apc EXCEPT ![a] = "done"]
       \/ /\ row' \in {row, [row EXCEPT ![a] = "live"]}
          /\ shared' = shared \ {a}
          /\ apc' = [apc EXCEPT ![a] = Thrown]
       \/ /\ LateCommit
          /\ row' = [row EXCEPT ![a] = "pending"]
          /\ apc' = [apc EXCEPT ![a] = Thrown]
          /\ UNCHANGED shared   \* the backend still holds its session lock
    /\ UNCHANGED <<tenant, obj, td, req, runVars, sweepVars, crashes>>

\* The backend of a COMMIT in flight ends it, and its session lock goes.
ResolveCommit(a) ==
    /\ row[a] = "pending"
    /\ row' \in {[row EXCEPT ![a] = "live"], [row EXCEPT ![a] = "none"]}
    /\ shared' = shared \ {a}
    /\ UNCHANGED <<tenant, obj, apc, td, req, runVars, sweepVars, crashes>>

\* The cancel of the caller, or the time limit of AddAsync, stops the save while it waits for the lock or loads the
\* workshop. The lock is freed. A cancel of the put or the commit is one of the throws of Put and CommitRow.
StopAdd(a) ==
    /\ apc[a] \in {"lock", "check"}
    /\ apc' = [apc EXCEPT ![a] = Thrown]
    /\ shared' = shared \ {a}
    /\ UNCHANGED <<tenant, row, obj, td, req, runVars, sweepVars, crashes>>

\* DeleteObjectWithNoRowAsync: AnyAsync with IgnoreQueryFilters on a new connection.
\* A pending COMMIT is not visible yet. A failed check keeps the object.
CleanupCheck(a) ==
    /\ apc[a] = "cleanCheck"
    /\ \/ /\ row[a] \in {"live", "deleted"}
          /\ apc' = [apc EXCEPT ![a] = "done"]
       \/ /\ row[a] \in {"none", "pending"}
          /\ apc' = [apc EXCEPT ![a] = "cleanDelete"]
       \/ /\ CleanupFails
          /\ apc' = [apc EXCEPT ![a] = "done"]
    /\ UNCHANGED <<tenant, row, obj, shared, td, req, runVars, sweepVars, crashes>>

\* DeleteObjectWithNoRowAsync: storage.DeleteAsync. A failure is logged and the object stays.
CleanupDelete(a) ==
    /\ apc[a] = "cleanDelete"
    /\ \/ obj' = [obj EXCEPT ![a] = FALSE]
       \/ CleanupFails /\ UNCHANGED obj
    /\ apc' = [apc EXCEPT ![a] = "done"]
    /\ UNCHANGED <<tenant, row, shared, td, req, runVars, sweepVars, crashes>>

\* The process dies. A put in flight can still land, a COMMIT in flight can still commit,
\* and a pending COMMIT keeps its backend. Postgres frees the session lock of the dead connection.
CrashAdder(a) ==
    /\ crashes < MaxCrashes
    /\ AddOwns(a, apc)
    /\ obj' \in IF apc[a] = "put" THEN {obj, [obj EXCEPT ![a] = TRUE]} ELSE {obj}
    /\ row' \in IF apc[a] = "commit" THEN {row, [row EXCEPT ![a] = "live"]} ELSE {row}
    /\ shared' = IF row[a] = "pending" THEN shared ELSE shared \ {a}
    /\ apc' = [apc EXCEPT ![a] = "crashed"]
    /\ crashes' = crashes + 1
    /\ UNCHANGED <<tenant, td, req, runVars, sweepVars>>

-----------------------------------------------------------------------------
(* The deletes. DeleteWorkshopAttachment, AttachmentStorage.DeleteAsync,   *)
(* DeleteTenant, and AppDbContext.PrepareWrite with ObjectDeletes.ForDeleted. *)

\* One save: the soft delete of the row and its DeleteObject job commit together.
DeleteAttachment(a) ==
    /\ tenant = "live" /\ row[a] = "live"
    /\ row' = [row EXCEPT ![a] = "deleted"]
    /\ req' = [req EXCEPT ![a] = NewRequest]
    /\ UNCHANGED <<tenant, obj, apc, shared, td, runVars, sweepVars, crashes>>

\* DeleteTenant: RootLock.Tenant.RunExclusiveAsync waits until no session holds the shared lock.
TakeExclusive ==
    /\ td = "none" /\ tenant = "live" /\ shared = {}
    /\ td' = "locked"
    /\ UNCHANGED <<tenant, row, obj, apc, shared, req, runVars, sweepVars, crashes>>

\* DeleteTenant: one save soft-deletes the tenant and each row it owns, and queues one DeleteTenantObjects job.
\* The attachments of the tenant get no DeleteObject job of their own.
CommitTenantDelete ==
    /\ td = "locked"
    /\ tenant' = "deleted"
    /\ row' = [a \in Atts |-> IF row[a] = "live" THEN "deleted" ELSE row[a]]
    /\ req' = [req EXCEPT ![Tenant] = NewRequest]
    /\ td' = "done"
    /\ UNCHANGED <<obj, apc, shared, runVars, sweepVars, crashes>>

CrashTenantDelete ==
    /\ crashes < MaxCrashes /\ td = "locked"
    /\ td' = "none"
    /\ crashes' = crashes + 1
    /\ UNCHANGED <<tenant, row, obj, apc, shared, req, runVars, sweepVars>>

-----------------------------------------------------------------------------
(* The BackWave worker group of the object-deletes queue. The backoff of   *)
(* the RetryPolicy only delays a claim, so the model lets a due job be     *)
(* claimed at any time.                                                    *)

Covers(q) == IF q = Tenant THEN Atts ELSE {q}

\* The BackWave claim: FOR UPDATE SKIP LOCKED takes a due job, adds one attempt, and gives a lease.
Claim(r, q) ==
    /\ rpc[r] = "idle" /\ req[q].st = "due"
    /\ req' = [req EXCEPT ![q] = [st |-> "leased", att |-> @.att + 1]]
    /\ rpc' = [rpc EXCEPT ![r] = "work"]
    /\ rq' = [rq EXCEPT ![r] = q]
    /\ ratt' = [ratt EXCEPT ![r] = req[q].att + 1]
    /\ UNCHANGED <<tenant, addVars, td, rlist, sweepVars, crashes>>

\* DeleteObjectHandler: storage.DeleteAsync. DeleteTenantObjectsHandler: the first ListObjectsV2Async of
\* DeleteUnderAsync. A throw or the TimeoutException of InAttemptTimeAsync fails the attempt.
Work(r) ==
    /\ rpc[r] = "work"
    /\ LET q == rq[r] IN
       IF q = Tenant
         THEN \/ /\ rlist' = [rlist EXCEPT ![r] = {a \in Atts : obj[a]}]
                 /\ rpc' = [rpc EXCEPT ![r] = "listed"]
                 /\ UNCHANGED obj
              \/ /\ rpc' = [rpc EXCEPT ![r] = "fail"]
                 /\ UNCHANGED <<obj, rlist>>
         ELSE \/ /\ obj' = [obj EXCEPT ![q] = FALSE]
                 /\ rpc' = [rpc EXCEPT ![r] = "complete"]
                 /\ UNCHANGED rlist
              \/ /\ rpc' = [rpc EXCEPT ![r] = "fail"]
                 /\ UNCHANGED <<obj, rlist>>
    /\ UNCHANGED <<tenant, row, apc, shared, td, req, rq, ratt, sweepVars, crashes>>

\* DeleteUnderAsync: DeleteObjectsAsync of the listed page. A failure can delete some keys of the page.
DeletePage(r) ==
    /\ rpc[r] = "listed"
    /\ \/ /\ obj' = [a \in Atts |-> obj[a] /\ a \notin rlist[r]]
          /\ rpc' = [rpc EXCEPT ![r] = "complete"]
       \/ /\ \E gone \in SUBSET rlist[r] : obj' = [a \in Atts |-> obj[a] /\ a \notin gone]
          /\ rpc' = [rpc EXCEPT ![r] = "fail"]
    /\ UNCHANGED <<tenant, row, apc, shared, td, req, rq, ratt, rlist, sweepVars, crashes>>

Release(r) ==
    /\ rpc' = [rpc EXCEPT ![r] = "idle"]
    /\ rq' = [rq EXCEPT ![r] = NoReq]
    /\ ratt' = [ratt EXCEPT ![r] = 0]
    /\ rlist' = [rlist EXCEPT ![r] = {}]

\* BackWave writes an outcome only while the claim still holds its lease. Each claim adds one attempt, so the
\* attempts name the claim.
Fenced(r) == req[rq[r]].st = "leased" /\ req[rq[r]].att = ratt[r]

\* The handler returns, and BackWave marks the job succeeded.
Complete(r) ==
    /\ rpc[r] = "complete"
    /\ req' = IF Fenced(r) THEN [req EXCEPT ![rq[r]].st = "gone"] ELSE req
    /\ Release(r)
    /\ UNCHANGED <<tenant, addVars, td, sweepVars, crashes>>

\* The handler throws. The RetryPolicy dead-letters the job at MaxAttempts, else it is due after a delay.
Fail(r) ==
    /\ rpc[r] = "fail"
    /\ req' = IF Fenced(r)
                THEN [req EXCEPT ![rq[r]].st = IF ratt[r] >= MaxAttempts THEN "dead" ELSE "due"]
                ELSE req
    /\ Release(r)
    /\ UNCHANGED <<tenant, addVars, td, sweepVars, crashes>>

\* The lease runs out: its worker died, or its heartbeat stopped. The model lets it run out at any time,
\* which covers a worker slower than its lease. The expired claim counts as an attempt, so the job is dead
\* at MaxAttempts.
LeaseExpires(q) ==
    /\ req[q].st = "leased"
    /\ req' = [req EXCEPT ![q].st = IF req[q].att >= MaxAttempts THEN "dead" ELSE "due"]
    /\ UNCHANGED <<tenant, addVars, td, runVars, sweepVars, crashes>>

\* The process of the worker dies, and a new process starts a new worker. The lease of its claim runs out.
CrashRunner(r) ==
    /\ crashes < MaxCrashes /\ rpc[r] # "idle"
    /\ Release(r)
    /\ crashes' = crashes + 1
    /\ UNCHANGED <<tenant, addVars, td, req, sweepVars>>

-----------------------------------------------------------------------------
(* The sweep, a recurring BackWave job. It lists the bucket under the      *)
(* prefix of the tenants, and it deletes each object older than the grace  *)
(* whose key no live row names. One page, as for the prefix delete.        *)

\* Time passes the grace of the object. With Grace = "add", the grace is the time limit of AddAsync plus a margin
\* for a COMMIT that ends after its cancel, so an object is old only after its add can no longer save its row.
\* "limit" is a grace with no margin: it passes after the add threw, also while its COMMIT is in flight.
\* "none" is a grace shorter than the time limit: it can pass at once.
Age(a) ==
    /\ obj[a] /\ ~old[a]
    /\ CASE Grace = "add" -> ~AddCanSave(a, apc, row)
         [] Grace = "limit" -> apc[a] \notin {"put", "lock", "check", "commit"}
         [] Grace = "none" -> TRUE
    /\ old' = [old EXCEPT ![a] = TRUE]
    /\ UNCHANGED <<tenant, addVars, td, req, runVars, spc, skeys, crashes>>

\* ListObjectsV2 gives a page of keys with LastModified. The sweep keeps the keys older than the grace.
SweepList ==
    /\ spc = "idle"
    /\ skeys' = {a \in Atts : obj[a] /\ old[a]}
    /\ spc' = "listed"
    /\ UNCHANGED <<tenant, addVars, td, req, runVars, old, crashes>>

\* The query of each attachment table for the listed keys. The tenant filter is off, the soft delete filter is on,
\* and a COMMIT in flight is not visible yet.
Names(a) == row[a] = "live" \/ (SoftDeletedNames /\ row[a] = "deleted")
SweepCheck ==
    /\ spc = "listed"
    /\ skeys' = {a \in skeys : ~Names(a)}
    /\ spc' = "checked"
    /\ UNCHANGED <<tenant, addVars, td, req, runVars, old, crashes>>

\* DeleteObjectsAsync of the keys that no row names.
SweepDelete ==
    /\ spc = "checked"
    /\ obj' = [a \in Atts |-> obj[a] /\ a \notin skeys]
    /\ spc' = "idle"
    /\ skeys' = {}
    /\ UNCHANGED <<tenant, row, apc, shared, td, req, runVars, old, crashes>>

\* The delete fails, and can still delete some of the keys. A process death of the sweep is the same step
\* with no key deleted, and the next run lists the bucket again.
SweepFail ==
    /\ spc = "checked"
    /\ \E gone \in SUBSET skeys : obj' = [a \in Atts |-> obj[a] /\ a \notin gone]
    /\ spc' = "idle"
    /\ skeys' = {}
    /\ UNCHANGED <<tenant, row, apc, shared, td, req, runVars, old, crashes>>

-----------------------------------------------------------------------------

Settled ==
    /\ \A a \in Atts : ~AddOwns(a, apc) /\ row[a] # "pending"
    /\ td # "locked"
    /\ \A r \in Runners : rpc[r] = "idle"
    /\ \A q \in Reqs : req[q].st \notin {"due", "leased"}

\* An end state: nothing is in flight. It changes nothing, so TLC does not call it a deadlock.
Done == Settled /\ UNCHANGED vars

AddStep(a) == Put(a) \/ TakeShared(a) \/ CheckWorkshop(a) \/ CommitRow(a) \/ CleanupCheck(a) \/ CleanupDelete(a)
RunStep(r) == Work(r) \/ DeletePage(r) \/ Complete(r) \/ Fail(r)

Next ==
    \/ \E a \in Atts : StartAdd(a) \/ Put(a) \/ TakeShared(a) \/ CheckWorkshop(a) \/ CommitRow(a)
                       \/ ResolveCommit(a) \/ StopAdd(a) \/ CleanupCheck(a) \/ CleanupDelete(a) \/ CrashAdder(a)
                       \/ DeleteAttachment(a)
    \/ TakeExclusive \/ CommitTenantDelete \/ CrashTenantDelete
    \/ \E r \in Runners : (\E q \in Reqs : Claim(r, q)) \/ Work(r) \/ DeletePage(r) \/ Complete(r)
                          \/ Fail(r) \/ CrashRunner(r)
    \/ \E q \in Reqs : LeaseExpires(q)
    \/ \E a \in Atts : Age(a)
    \/ SweepList \/ SweepCheck \/ SweepDelete \/ SweepFail
    \/ Done

\* Each step that the code takes on its own happens. A user who starts an add or a delete need not. Time passes,
\* the schedule starts the sweep again, and a storage that fails works again at some time.
Fairness ==
    /\ \A a \in Atts : WF_vars(AddStep(a)) /\ WF_vars(ResolveCommit(a)) /\ WF_vars(Age(a))
    /\ WF_vars(CommitTenantDelete)
    /\ \A r \in Runners : WF_vars(\E q \in Reqs : Claim(r, q)) /\ WF_vars(RunStep(r))
    /\ \A q \in Reqs : WF_vars(LeaseExpires(q))
    /\ WF_vars(SweepList) /\ WF_vars(SweepCheck) /\ SF_vars(SweepDelete)

Spec == Init /\ [][Next]_vars /\ Fairness

-----------------------------------------------------------------------------
(* Safety. The harm: a user sees a file in the list that no one can download. *)

\* A live row of a live tenant always has its object in the bucket.
NoLiveRowWithoutObject == \A a \in Atts : (tenant = "live" /\ row[a] = "live") => obj[a]

\* After a tenant delete commits, no row of the tenant is live, so none can lose its object to the prefix delete.
NoLiveRowInDeletedTenant == tenant = "deleted" => \A a \in Atts : row[a] \notin {"live", "pending"}

(* A witness, not a rule. It breaks when an object stays under a deleted tenant after its DeleteTenantObjects *)
(* job succeeded, so TLC shows the path. OrphansGo then shows that the sweep deletes that object.            *)
NoObjectLeftInDeletedTenant ==
    (tenant = "deleted" /\ req[Tenant].st = "gone") => \A a \in Atts : obj[a] => AddOwns(a, apc)

(* Liveness. A way out of each state that is not an end state. *)

\* Each job ends: it succeeds, or it is dead.
RequestsSettle == \A q \in Reqs : (req[q].st \in {"due", "leased"}) ~> (req[q].st \in {"gone", "dead"})
\* Each add that started returns, throws, or dies with its process.
AddsSettle == \A a \in Atts : AddOwns(a, apc) ~> (apc[a] \in {"done", "crashed"})
\* A COMMIT in flight ends, and a tenant delete that holds the lock ends.
CommitsSettle == \A a \in Atts : (row[a] = "pending") ~> (row[a] # "pending")
TenantDeleteSettles == (td = "locked") ~> (td # "locked")

(* Coverage. The harm: storage that is paid for and that no code will ever delete. *)

\* Each object that no live row names and that no add can still save a row for is deleted.
OrphansGo == \A a \in Atts : Orphan(a, obj, row, apc) ~> ~obj[a]
=============================================================================
