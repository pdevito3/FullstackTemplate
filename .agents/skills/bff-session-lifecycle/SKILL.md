---
name: bff-session-lifecycle
description: Model-checks the BFF session lifecycle with TLA+ and TLC - the login that creates the row, the token refresh under the refresh lock, logout, back-channel logout, the cleanup, and a crash of a BFF instance between any two steps. Use when you change FullstackTemplate.Bff/Sessions, the refresh lock, LogoutEndpoint, or BackChannelLogoutEndpoint, or when you ask whether a logout can miss a session, a refresh can hang, or a refresh token can leak.
---

# BFF session lifecycle

The model is `model/BffSession.tla`. It describes how the rows of the `sessions` table, the `pg_advisory_xact_lock` of the refresh lock, the `_inFlight` map of each instance, the `used_logout_tokens` row, and the refresh tokens at the identity provider (IdP) change. Two BFF instances share one `bffdb`. Each action is one atomic step in the code: one SQL statement in autocommit, one commit, one call to the IdP, or one in-memory step. Each action has a comment that names its code. An instance can crash between any two actions. A crash stops its requests, Postgres rolls back their transactions and frees their locks, and `_inFlight` starts empty.

Read `docs/specs/home-rolled-bff.md` sections 3.1, 3.4, and 3.7 before you change the model.

## The rules

| Rule | In plain words | The harm if it breaks |
|---|---|---|
| `LogoutEndsSession` | After `/bff/logout` completes, no readable row has the key of that BFF session. | The user logs out, and the old cookie still works. |
| `BackChannelLogoutEndsSessions` | After `/bff/backchannel` answers 200 for a `sid`, no readable row has that `sid`. | An admin ends the IdP session, and the BFF still shows the user as signed in. |
| `ReplayRefusedOnlyWhenEnded` | The BFF refuses a logout token as a replay only when no readable row of its `sid` is left. | The IdP sends the token again after a failure, the BFF answers 400, and the session never ends. |
| `NoUnexcusedOrphanToken` | The IdP never holds a live refresh token that no row and no running step holds, except in one of the allowed windows below. | A refresh token stays valid at the IdP that nobody will ever use or revoke. |
| `NoTokenWriteConflict` | A token write inside the refresh lock never finds other tokens in the row (`BffTicketStore.cs` throws there). | A refresh fails with an exception. |
| `NoStaleInFlightEntry`, `NoStaleLock` | An `_inFlight` entry, an advisory lock, and a row lock always belong to a running step. | A later refresh waits for a step that no longer exists. |
| `RefreshEnds` | Every refresh in flight ends: success, a temporary failure, a permanent failure, or the crash of its instance. | A request hangs. |
| `BackChannelEnds` | Every back-channel handling ends with an answer or with a crash. | The IdP waits for an answer that never comes. |
| `NoDeadlock` | No state is stuck. The `Finished` action marks the real end states. | The model or the code can stop. |

The allowed windows of `NoUnexcusedOrphanToken`. The ghost variable `why` records which one left a token live with no row. Only these actions set it:

1. **The refresh response was lost.** The IdP rotated the token, and the response did not arrive, or `RefreshTimeout` fired first (spec 3.7). The row keeps the used token, and the next refresh gets `invalid_grant`.
2. **The lock transaction ended before its commit.** The IdP rotated the token, and the database failed before the commit (`RetryAfterRefresh`, spec 3.7, Cancellation).
3. **A crash before the row write or the revocation.** A crash after the IdP rotated the token and before the commit (spec 3.7). A crash after the code exchange and before `BffTicketStore.StoreAsync` creates the row. A crash after the logout delete and before the revocation.
4. **A revocation failed.** `RefreshTokenRevocation` logs the error and goes on, at logout, at a token write that finds no row, and after a rejected ID token (spec 3.4 step 4).
5. **A same-sid login keeps the old token.** `NewLogin.RevokeRefreshTokenAsync` keeps it by design (spec 3.1). The end of the IdP session ends it.
6. **The cleanup deleted an expired row.** `BffSessionCleanupService` does not revoke.

If a change adds another way to leave a live token with no row, `NoUnexcusedOrphanToken` breaks. Then decide if the new window is allowed. If it is, add it as a new `why` value in the action that opens it, and list it here. Do not set `why` in a normal step.

## Run

Run `mise trust` once in the `model` folder. Then run this command from the repo root:

```sh
.agents/skills/bff-session-lifecycle/model/check.sh
```

`model/mise.toml` pins TLC 1.7.4 and Java Temurin 25. The script checks each config, and it must print only `ok` lines. It takes about 2 to 4 minutes on a laptop, most of it for `Refresh.cfg`. For each config, the rules hold, except the findings below, and each finding must still break. The output files are in `$TMPDIR/bff-session-lifecycle/`.

| Config | Scope | Expected |
|---|---|---|
| `Refresh.cfg` | 3 requests (2 on instance 1, 1 on instance 2), 1 login, `/bff/logout`, expiry renewals, expiry and cleanup, 3 refresh tokens, 1 crash. Safety only, about 1.8 million states. | All rules hold. |
| `RefreshLiveness.cfg` | The same requests, 2 refresh tokens, no logout, no renewal, no expiry, 1 crash. | All rules hold, `RefreshEnds` too. |
| `BackChannel.cfg` | 1 request, 2 logins of one browser with the same `sid`, 2 deliveries of the logout token, 1 crash. | G1 breaks. The other rules hold. |
| `BffSessionChart.cfg` | 1 request, 1 login, 1 crash, no logout. About 170 states, for the chart. | All rules hold. |

Read every result with the `/statechart` skill. Give it `model/BffSession.tla`, the config, and `model/view.mjs`. For a rule that breaks, it gives the shortest path in plain words. For `BffSessionChart.cfg`, it draws the chart.

## Model error or code bug

When a rule breaks, replay the path in the code, one action at a time. Each action comment names the line. Then decide:

- **A code bug.** Each step of the path can happen in the code in that order. Report it as a finding with the path in the words of the code. Do not weaken the rule or the model to make it pass, and do not change product code unless the user asks.
- **A model error.** A step of the path cannot happen in the code. For example, the model lets two statements run as one step, or lets a step run that the code guards. Correct the action so that it matches the code, and name the code in its comment.

When you change the code on purpose, change the model in the same commit. Then run `check.sh`, and change the expected findings in `check.sh` only when a finding is fixed or a new one is a real bug.

## Coverage

Sync commit: `b43c7779`. The model describes these files at this commit:

- `FullstackTemplate.Bff/Sessions/NewLogin.cs`: `EndExistingSessionAsync`, `RevokeRefreshTokenAsync`
- `FullstackTemplate.Bff/Sessions/BffTicketStore.cs`: `StoreAsync`, `RetrieveAsync`, `RenewAsync`, `RenewExpiryAsync`, `RenewTokensAsync`, `RemoveAsync`
- `FullstackTemplate.Bff/Sessions/EfBffSessionStore.cs`: `CreateAsync`, `GetAsync`, `RenewAsync`, `RenewExpiryAsync`, `DeleteAsync`, `FindBySessionIdAsync`, `FindBySubjectAsync`, `DeleteEndedAsync`, `WriteAsync`
- `FullstackTemplate.Bff/Sessions/BffSessionCleanupService.cs`: `CleanupAsync`
- `FullstackTemplate.Bff/Authentication/RefreshLock.cs`: `ExecuteWithConcurrencyControlAsync`, `WaitForRefreshAsync`, `RefreshInLockAsync`, `TryLockAsync`
- `FullstackTemplate.Bff/Authentication/RefreshTimeout.cs`, `RefreshTokenRevocation.cs`, `RefreshIdTokenValidation.cs` (only the revoke of a rejected response)
- `FullstackTemplate.Bff/Proxy/ApiProxy.cs`: the token attach and its sign-out after a permanent failure
- `FullstackTemplate.Bff/Logout/LogoutEndpoint.cs`: `HandleAsync`
- `FullstackTemplate.Bff/BackChannel/BackChannelLogoutEndpoint.cs`: `HandleAsync`, `LimitIdleTimeAsync`, `CommittedAsync`, `DeleteSessionsAsync`
- `FullstackTemplate.Bff/BackChannel/LogoutTokenReplayCache.cs`: `TryAddAsync`
- `FullstackTemplate.Bff/Sessions/BffSessionExtensions.cs`: `AddBffSessions` (the retry of `25P03`)

To see if the model needs another look, run `git log --oneline b43c7779..HEAD -- <the files above>`. Move the sync commit forward only after you confirm that the model describes the code at the new commit. The `/maintain-formal-verification-skill` skill does this work.

The model leaves out: the token values, the cookie chunks, the CSRF and `sid` checks of `/bff/logout`, the validation of the logout token, DPoP, the clock (expiry is one event per row), a login with a new `sid` (so the revoke branch of `NewLogin` is not checked), a second delivery of the logout token while the first runs, and a second crash. A pass covers only these bounds.

## Findings at the sync commit

**G1: a back-channel logout that comes before the login row commits is lost.** `BackChannelLogoutEndsSessions` breaks. The path:

1. The user signs in at the IdP, and `/signin-oidc` exchanges the code for tokens.
2. The IdP session ends (an admin ends it), and the IdP sends the logout token.
3. `BackChannelLogoutEndpoint` writes the jti, and `FindBySessionIdAsync` finds no row, because the login did not create it yet.
4. `BffTicketStore.StoreAsync` creates the row with that `sid`.
5. The back-channel logout answers 200.

The IdP thinks the session ended, but the BFF shows the user as signed in until the next refresh gets `invalid_grant` or the row expires. Nothing at login checks for a logout of its `sid` that came first.

**G2 is fixed.** A resend after a failure was refused as a replay, because `TryAddAsync` committed the jti before the deletes. Since `24aa1542`, the jti insert and the deletes commit in one transaction, so a crash or a failed delete rolls back the jti too, and the resend ends the sessions. `ReplayRefusedOnlyWhenEnded` holds.

## Proof that the check can fail

At the sync commit, a scratch change in `BffTicketStore.RenewTokensAsync` made the `Missing` case call `CreateAsync` and create the row again, as the code did before the conditional writes. It built. The same change in `ReqWritesTokens` made `check.sh` print `FAIL` for `Refresh.cfg`. `/statechart` gave this path for `LogoutEndsSession`: a request takes the refresh lock, reads the row again, and the IdP rotates the token. Then `/bff/logout` deletes the row, the token write finds no row and creates it, the lock transaction commits, and logout completes with a readable row. Both changes were then reverted, and `check.sh` printed only `ok` lines.

At `b43c7779`, a scratch change in `BcInsertsJti` committed the jti at its insert, as the code did before `24aa1542`. TLC then broke `ReplayRefusedOnlyWhenEnded` with the constants of `BackChannel.cfg`, in a path of 10 states: the login creates the row, the jti commits, the instance crashes before the deletes, and the resend gets 400 with the row still readable. The change was then reverted.

While G1 is open, `BackChannel.cfg` cannot show a new break of `BackChannelLogoutEndsSessions`, because the script expects that rule to break there. `Refresh.cfg` checks `LogoutEndsSession`, which catches the same kind of break. After a G1 fix, remove `BackChannelLogoutEndsSessions` from the expected findings in `check.sh`.
