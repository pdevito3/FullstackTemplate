---
name: membership-lifecycle
description: Model-checks tenant membership with TLA+ and TLC - the invite lifecycle (Pending, Claimed, Revoked, and the computed expiry), user provisioning, user delete, and tenant delete - over every order of the concurrent requests. Use when you change ClaimInvite, RevokeInvite, Invite, InviteStatus, InitiateUserAndTenant, DeleteUser, DeleteTenant, TenantOwnedRows, RootLock, RaceRetryBehavior, the xmin version convention, or the users.identifier index, or when you ask whether a race in these flows can make a second user, a user in a deleted tenant, or an invite with two outcomes.
---

# Membership lifecycle

A TLA+ model of the requests that add a user to a tenant or take one out. TLC checks every order of their steps, with crashes, cancels, and timeouts at any step. Spec rules: INV-4 to INV-7, INV-10, INV-11, and UM-3 in `docs/specs/user-invites.md`.

## The rules

| Rule (model name) | Plain sentence | Harm when it breaks |
|---|---|---|
| `OneLiveUserPerSub` | A sign-in identity (`sub`) has at most one live user. | One person is in two tenants, and the tenant filter picks one at random. |
| `LiveUserInLiveTenant` | Each live user is in a live tenant. | A user of a deleted tenant can sign in to an empty tenant. |
| `OneOutcomePerInvite` | An invite leaves Pending at most one time. | An admin revokes an invite and the colleague is in the tenant anyway, or the reverse. |
| `ClaimedMeansOneUser` | A Claimed invite gave exactly one user, the one in `ClaimedByUserId`. An invite that is not Claimed gave no user. | One link adds two people. |
| `RevokeBeatsClaim` | A claim that a revoke beat after the token lookup or the load adds no user. It gets 410, or 404 when a tenant delete also took the invite, or 409 when the sub got a user meanwhile. | A revoked link still adds a user. |
| `RunsBounded` | Each run-again loop stops at `MaxRuns`. | A request loops without end. |
| `EveryRequestEnds` (liveness) | Each request gets an answer: a success, a 4xx, a 409 after `MaxRuns`, or a failure. No request waits on a lock or loops forever. | A request hangs. |
| `PendingHasWayOut` (liveness) | A Pending invite always gets out: a claim, a revoke, the expiry, or the tenant delete. | An invite stays claimable forever. |

## What the model treats as one step

Each action in `model/Membership.tla` names its code. One action is one transaction or one query that `PauseAtQuery` can stop at.

| Action | Code |
|---|---|
| `ClaimFindTenant` | `ClaimInvite.Handler`: the live-user pre-check, then the `invites.claim.find_tenant` lookup, with no lock |
| `ClaimLoad` | `RootLock.Tenant.RunSharedAsync`, the `invites.claim.load` query, and `Invite.Claim` in memory (`GuardClaimable`) |
| `ClaimSave` | The one `SaveChangesAsync`: user insert, invite update with the xmin check, the `ix_users_identifier` index. On `StaleVersionException`, `RaceRetryBehavior` runs the handler again from `ClaimFindTenant` |
| `RevokeLoad`, `RevokeSave` | `RevokeInvite.Handler`: the `invites.revoke.load` query and `Invite.Revoke`, then the save with the xmin check. No lock |
| `DeleteLoad`, `DeleteSave` | `DeleteTenant.Handler`: the exclusive lock, `TenantOwnedRows.LoadAsync`, then one save that soft-deletes the loaded rows and the tenant. A soft delete is an update with the xmin check (`AppDbContext`, `EntityState.Deleted` to `Modified`) |
| `DeleteUser` | `DeleteUser.Handler`, load and soft delete as one step. The soft delete frees the identifier |
| `InitiateCheck`, `InitiateSave` | `InitiateUserAndTenant.Handler`: the `users.initiate.identifier_in_use` check, then the save of the tenant and the user, with the index as the backstop |
| `ClockPassesExpiry` | Time passes `ExpiresAt`. `Invite.IsExpiredAt` computes the expiry, so no process must run for it |
| `*Fails` | A crash, a cancel, a timeout, or a restart. The transaction rolls back and the session lock ends with its connection |

## Bounds and what the model leaves out

- `MC.cfg` (safety, about 1.3 million states, 20 s): subs s1 and s2 both claim invite i1, s1 also claims i2, an admin revokes i1, s2 starts its own tenant, an admin deletes tenant t1 and can remove any user. `MaxRuns = 3`.
- `Live.cfg` (liveness, about 230,000 states, 100 s): the same with no user delete and `MaxRuns = 2`.
- `Chart.cfg` (384 states): one claim, one revoke, one tenant delete, on one invite. `/statechart` draws it.
- The code has `MaxRuns = 20`. The model uses 2 or 3, because a run again only repeats steps that the model already covers.
- Left out: the email match (INV-9, a 403 with no write), `AddInvite` (invites exist at the start), tenant names, the client that sends a request again after a failure, the fairness of the Postgres lock queue, and the execution strategy's own run again after a transient failure (the model shows it as a failure and a new run would be a new request).
- Liveness assumes weak fairness: a request that can take its next step takes it in the end, and the clock keeps moving.

## Run

From the repo root:

```bash
.agents/skills/membership-lifecycle/check.sh
```

It installs the pinned TLC and Java from `model/mise.toml` (tla2tools 1.7.4, temurin-25), runs the three configs, and prints `PASS` or `FAIL` per config with the TLC log path. About 2.5 minutes. To run one config: `check.sh MC.cfg`.

Read each result with the `/statechart` skill: run it on `model/MC.tla` with `--config model/<CONFIG>` and `--view model/view.mjs`, and an `--out` folder outside the repo. On a failure it gives the shortest path for each rule that breaks, in the words of `view.mjs`. On `Chart.cfg` it also draws the chart of the invite's states. `MC.cfg` is too large for a chart, and `/statechart` checks it with one worker, so it is slower than `check.sh`.

## Is it a model error or a code bug?

When a rule breaks:

1. Read the path from `/statechart`. Replay it against the real code as an integration race test with `PauseAtQuery` (see `docs/backend.md`, "Race tests"): one step of the path for each paused query. The tests in `tests/FullstackTemplate.IntegrationTests/FeatureTests/Invites/ClaimInviteCommandTests.cs` show the pattern for each race in the model.
2. If the test shows the bug, it is a code bug. Report it with the path in the code's words. Do not weaken the rule or the model.
3. If the test passes, the model is wrong until you show otherwise. Check that the action matches its code row above, mainly what is in one transaction and which writes have the xmin check.
4. A TLC error such as "not completely specified" or a type error is a model error, not a rule break.

## When the code changes

Compare the covered files below with the sync commit: `git diff <sync commit> -- <files>`. For each change, update the action in the table and in `Membership.tla`, run `check.sh`, and move the sync commit forward only after you confirm that the model describes the code at the new commit. To prove that a rule can still fail, switch `InviteVersionCheck` or `ClaimTakesLock` to `FALSE` in a config, see the failure, and switch it back.

## Coverage record

Sync commit: `d3bac1b6` (the model describes these files at that commit).

- `FullstackTemplate.Server/Domain/Invites/Features/ClaimInvite/ClaimInvite.cs` - `Handler.Handle`
- `FullstackTemplate.Server/Domain/Invites/Features/RevokeInvite/RevokeInvite.cs` - `Handler.Handle`
- `FullstackTemplate.Server/Domain/Invites/Invite.cs` - `IsExpiredAt`, `GuardClaimable`, `Claim`, `Revoke`, `MoveTo`
- `FullstackTemplate.Server/Domain/Invites/InviteStatus.cs` - the `Next` of each status
- `FullstackTemplate.Server/Domain/Users/Features/InitiateUserAndTenant/InitiateUserAndTenant.cs` - `Handler.Handle`
- `FullstackTemplate.Server/Domain/Users/Features/DeleteUser/DeleteUser.cs` - `Handler.Handle`
- `FullstackTemplate.Server/Domain/Tenants/Features/DeleteTenant/DeleteTenant.cs` - `Handler.Handle`
- `FullstackTemplate.Server/Databases/TenantOwnedRows.cs` - `LoadAsync`
- `FullstackTemplate.Server/Databases/RootLock.cs` - `RunSharedAsync`, `RunExclusiveAsync`, `FreeAsync`
- `FullstackTemplate.Server/Resources/RaceRetryBehavior.cs` - `Handle`, `MaxRuns`
- `FullstackTemplate.Server/Databases/BaseEntityConventions.cs` - `VersionToXminConvention`
- `FullstackTemplate.Server/Databases/AppDbContext.cs` - the soft delete of an `EntityState.Deleted` entry
- `FullstackTemplate.Server/Databases/EntityConfigurations/UserConfiguration.cs` - the unique filtered index on `Identifier`

## Findings (at the sync commit)

- All rules hold at the bounds above. No reachable bug.
- The shared tenant lock of the claim is a second guard. With `ClaimTakesLock = FALSE`, every rule still holds, because the tenant delete soft-deletes each invite with an xmin-checked update, and the claim updates the same invite with an xmin check. So whichever save comes second fails its version check and runs again. The lock still makes the delete wait, which `a_tenant_delete_during_a_claim_waits_and_then_deletes_the_new_user` tests. Only the model shows this, not a test of the code.
- A claim can commit just after `ExpiresAt`: the check of the expiry happens when the claim loads the invite, and the save comes later in the same request. The chart shows this as the arrow from Expired to Claimed. The window is the length of one request.
- Proof that the check can fail: with the xmin check off for `Invite` in the code (`builder.Property(e => e.Version).IsConcurrencyToken(false)` in `InviteConfiguration`), the race tests `a_claim_that_a_rival_claim_beat_after_the_load_fails_with_gone` and `a_claim_that_a_revoke_beat_after_the_load_fails_with_gone` fail. With `InviteVersionCheck = FALSE`, `OneOutcomePerInvite`, `ClaimedMeansOneUser`, and `RevokeBeatsClaim` break. Shortest path: the claim looks up the token, the claim loads the invite, the revoke loads the invite, the revoke commits, the claim commits and adds the user.
