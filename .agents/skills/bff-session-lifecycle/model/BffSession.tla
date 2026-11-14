---------------------------- MODULE BffSession ----------------------------
(***************************************************************************)
(* The BFF session lifecycle of FullstackTemplate.Bff: login creates the   *)
(* row, the refresh lock rotates the refresh token, logout and back-channel *)
(* logout delete rows, and the cleanup deletes expired rows. Two BFF        *)
(* instances share one bffdb. An instance can crash between any two steps.  *)
(*                                                                          *)
(* Each action names the code that it models. One action is one atomic     *)
(* step in the code: one SQL statement in autocommit, one commit, one call  *)
(* to the identity provider, or one in-memory step.                         *)
(***************************************************************************)
EXTENDS Naturals, FiniteSets

CONSTANTS
    NReqs,                \* API requests that can refresh. Request NReqs runs on instance 2, the others on instance 1.
    Logins,               \* logins of the one browser (each login creates one row, key = its number)
    MaxTokens,            \* refresh tokens that the identity provider can issue
    MaxCrashes,           \* instance crashes
    MaxDeliveries,        \* deliveries of the logout token by the identity provider
    WithExpiry,           \* rows can expire, and the cleanup deletes them
    WithRenewal,          \* a request can renew only the expiry (RenewExpiryAsync), which moves xmin
    WithLogout            \* the browser calls /bff/logout one time

Reqs     == 1..NReqs
Keys     == 1..Logins
Tokens   == 1..MaxTokens
Inst(r)  == IF r = NReqs THEN 2 ELSE 1
LoginInst == 1   \* the /signin-oidc callback
LogoutInst == 2  \* /bff/logout
BcInst == 2      \* /bff/backchannel
BC == NReqs + 1  \* the back-channel request, as a holder of row locks
WriteAttempts == 3  \* BffTicketStore.WriteAttempts

NoRow == [exists |-> FALSE, rt |-> 0, ver |-> 0, expired |-> FALSE]

VARIABLES
    \* identity provider
    idp,        \* the IdP session S: "none", "live", "ended". One sid and one sub.
    rt,         \* rt[t]: "unissued", "live", "used" (rotated), "revoked", "dead" (its IdP session ended)
    nextRt,
    \* bffdb
    row,        \* row[k]: the committed sessions row of key k. ver is xmin.
    rowLock,    \* rowLock[k]: the transaction that holds the Postgres row lock of k (an uncommitted UPDATE or DELETE), or 0
    lock,       \* lock[k]: the request that holds pg_advisory_xact_lock of k, or 0
    jtiUsed,    \* the committed used_logout_tokens row of the jti of S
    \* instances
    inflight,   \* inflight[i][t]: the leader request of RefreshLock._inFlight for refresh token t on instance i, or 0
    crashes,
    \* the login callback (OIDC handler, NewLogin, BffTicketStore.StoreAsync)
    lpc, ltok, cookie, logins,
    \* API requests (ApiProxy -> token management -> RefreshLock -> BffTicketStore.RenewAsync)
    pc, key, start, ver, newTok, tries, pend,
    \* /bff/logout (LogoutEndpoint)
    opc, okey, otok, loggedOut,
    \* /bff/backchannel (BackChannelLogoutEndpoint)
    bpc, found, bcDel, jtiPend, deliveries, bcOk,
    \* ghosts
    why,              \* why[t]: the allowed window that left t live with no row, or "none"
    refusedWithRows   \* the BFF refused the logout token as a replay while a readable row of S existed

idpVars == <<idp, rt, nextRt>>
dbVars  == <<row, rowLock, lock, jtiUsed>>
lVars   == <<lpc, ltok, cookie, logins>>
rVars   == <<pc, key, start, ver, newTok, tries, pend>>
oVars   == <<opc, okey, otok, loggedOut>>
bVars   == <<bpc, found, bcDel, jtiPend, deliveries, bcOk>>
vars == <<idpVars, dbVars, inflight, crashes, lVars, rVars, oVars, bVars, why, refusedWithRows>>

Readable(k) == k \in Keys /\ row[k].exists /\ ~row[k].expired     \* EfBffSessionStore.GetAsync filters Expires and AbsoluteExpires
LiveRt(t) == t \in Tokens /\ rt[t] = "live"
\* A token that dies or is revoked needs no excuse. One that stays live with no holder does.
Excuse(t, reason) == IF LiveRt(t) /\ why[t] = "none" THEN [why EXCEPT ![t] = reason] ELSE why
Revoke(t) == IF LiveRt(t) THEN [rt EXCEPT ![t] = "revoked"] ELSE rt   \* RefreshTokenRevocation.RevokeAsync
Issue == [rt EXCEPT ![nextRt] = "live"]

TypeOK ==
    /\ idp \in {"none", "live", "ended"}
    /\ rt \in [Tokens -> {"unissued", "live", "used", "revoked", "dead"}]
    /\ row \in [Keys -> [exists : BOOLEAN, rt : 0..MaxTokens, ver : Nat, expired : BOOLEAN]]
    /\ rowLock \in [Keys -> 0..BC]
    /\ lock \in [Keys -> 0..NReqs]
    /\ inflight \in [{1, 2} -> [Tokens -> 0..NReqs]]
    /\ pc \in [Reqs -> {"idle", "refresh", "waiter", "lockwait", "locked", "calling", "write",
                        "revokeNew", "rejectRevoke", "permdelete", "commit", "end", "renew", "fault", "done"}]

Init ==
    /\ idp = "none" /\ rt = [t \in Tokens |-> "unissued"] /\ nextRt = 1
    /\ row = [k \in Keys |-> NoRow] /\ rowLock = [k \in Keys |-> 0] /\ lock = [k \in Keys |-> 0]
    /\ jtiUsed = FALSE
    /\ inflight = [i \in {1, 2} |-> [t \in Tokens |-> 0]] /\ crashes = 0
    /\ lpc = "start" /\ ltok = 0 /\ cookie = 0 /\ logins = 0
    /\ pc = [r \in Reqs |-> "idle"] /\ key = [r \in Reqs |-> 0] /\ start = [r \in Reqs |-> 0]
    /\ ver = [r \in Reqs |-> 0] /\ newTok = [r \in Reqs |-> 0] /\ tries = [r \in Reqs |-> 0]
    /\ pend = [r \in Reqs |-> "none"]
    /\ opc = "idle" /\ okey = 0 /\ otok = 0 /\ loggedOut = 0
    /\ bpc = "idle" /\ found = {} /\ bcDel = {} /\ jtiPend = FALSE /\ deliveries = 0 /\ bcOk = FALSE
    /\ why = [t \in Tokens |-> "none"] /\ refusedWithRows = FALSE

-----------------------------------------------------------------------------
(* The identity provider *)

\* An admin ends the IdP session, or the end_session_endpoint does after /bff/logout.
\* Every refresh token of S dies, and the IdP starts to send the logout token.
IdpEndsSession ==
    /\ idp = "live"
    /\ idp' = "ended"
    /\ rt' = [t \in Tokens |-> IF rt[t] = "live" THEN "dead" ELSE rt[t]]
    /\ UNCHANGED <<nextRt, dbVars, inflight, crashes, lVars, rVars, oVars, bVars, why, refusedWithRows>>

-----------------------------------------------------------------------------
(* Login: the OIDC handler, NewLogin.EndExistingSessionAsync, BffTicketStore.StoreAsync *)

\* The user signs in at the IdP (/bff/login, then the authorize page). A second login joins S (SSO, same sid).
LoginAuthorize ==
    /\ lpc = "start" /\ logins < Logins /\ idp \in {"none", "live"}
    /\ idp' = "live" /\ lpc' = "authorized"
    /\ UNCHANGED <<rt, nextRt, dbVars, inflight, crashes, ltok, cookie, logins, rVars, oVars, bVars, why, refusedWithRows>>

\* /signin-oidc: the code exchange. The IdP issues the first refresh token of the login.
LoginExchange ==
    /\ lpc = "authorized" /\ idp = "live" /\ nextRt <= MaxTokens
    /\ rt' = Issue /\ nextRt' = nextRt + 1 /\ ltok' = nextRt /\ lpc' = "exchanged"
    /\ UNCHANGED <<idp, dbVars, inflight, crashes, cookie, logins, rVars, oVars, bVars, why, refusedWithRows>>

\* The code exchange fails, because S ended. No row.
LoginExchangeFails ==
    /\ lpc = "authorized" /\ idp = "ended"
    /\ lpc' = "start"
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, ltok, cookie, logins, rVars, oVars, bVars, why, refusedWithRows>>

\* NewLogin.cs:37 DeleteAsync of the old row. The new login has the same sid, so RevokeRefreshTokenAsync
\* keeps the old refresh token (NewLogin.cs:59). The end of S ends it.
LoginEndsOldSession ==
    /\ lpc = "exchanged" /\ cookie # 0 /\ rowLock[cookie] = 0
    /\ row' = [row EXCEPT ![cookie].exists = FALSE]
    /\ why' = IF row[cookie].exists THEN Excuse(row[cookie].rt, "a same-sid login keeps the old token") ELSE why
    /\ cookie' = 0
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, lpc, ltok, logins, rVars, oVars, bVars, refusedWithRows>>

\* BffTicketStore.cs:69 CreateAsync. With a cross-site IdP the cookie does not come, so the old row can stay.
LoginCreatesRow ==
    /\ lpc = "exchanged"
    /\ LET k == logins + 1 IN
         /\ row' = [row EXCEPT ![k] = [exists |-> TRUE, rt |-> ltok, ver |-> 0, expired |-> FALSE]]
         /\ cookie' = k
    /\ logins' = logins + 1 /\ ltok' = 0 /\ lpc' = "start"
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, rVars, oVars, bVars, why, refusedWithRows>>

-----------------------------------------------------------------------------
(* API requests: ApiProxy.cs:112 GetUserAccessTokenAsync, RefreshLock, BffTicketStore.RenewAsync *)

\* BffTicketStore.RetrieveAsync: the cookie handler reads the row, and BffSessionRequest remembers its tokens and xmin.
\* The request then needs a refresh, or renews only the expiry.
ReqReads(r) ==
    /\ pc[r] = "idle"
    /\ IF Readable(cookie)
         THEN /\ key' = [key EXCEPT ![r] = cookie]
              /\ start' = [start EXCEPT ![r] = row[cookie].rt]
              /\ ver' = [ver EXCEPT ![r] = row[cookie].ver]
              /\ pc' = [pc EXCEPT ![r] = "refresh"] \/ (WithRenewal /\ pc' = [pc EXCEPT ![r] = "renew"])
         ELSE pc' = [pc EXCEPT ![r] = "done"] /\ UNCHANGED <<key, start, ver>>
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* RefreshLock.cs:92 _inFlight.GetOrAdd: the first request with this refresh token leads, the others wait.
ReqJoinsInFlight(r) ==
    /\ pc[r] = "refresh"
    /\ LET i == Inst(r) IN
       IF inflight[i][start[r]] = 0
         THEN /\ inflight' = [inflight EXCEPT ![i][start[r]] = r]
              /\ pc' = [pc EXCEPT ![r] = "lockwait"]
         ELSE /\ pc' = [pc EXCEPT ![r] = "waiter"] /\ UNCHANGED inflight
    /\ UNCHANGED <<idpVars, dbVars, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* RefreshLock.WaitForRefreshAsync: the result of the leader, or a timeout at InProcessWaitLimit (temporary).
ReqWaiterEnds(r) ==
    /\ pc[r] = "waiter"
    /\ pc' = [pc EXCEPT ![r] = "done"]
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* RefreshLock.cs:183-184: BeginTransaction, then pg_advisory_xact_lock.
ReqTakesLock(r) ==
    /\ pc[r] = "lockwait" /\ lock[key[r]] = 0
    /\ lock' = [lock EXCEPT ![key[r]] = r]
    /\ pc' = [pc EXCEPT ![r] = "locked"]
    /\ UNCHANGED <<idpVars, row, rowLock, jtiUsed, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* lock_timeout = LockTimeout: a temporary failure (Outcome.LockTimeout).
ReqLockTimesOut(r) ==
    /\ pc[r] = "lockwait"
    /\ pc' = [pc EXCEPT ![r] = "end"]
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* RefreshLock.cs:187-202: read the row again inside the lock.
ReqRereads(r) ==
    /\ pc[r] = "locked"
    /\ IF Readable(key[r]) /\ row[key[r]].rt = start[r]
         THEN /\ ver' = [ver EXCEPT ![r] = row[key[r]].ver]   \* request.Remember(row, storedTokens)
              /\ pc' = [pc EXCEPT ![r] = "calling"]
         ELSE /\ pc' = [pc EXCEPT ![r] = "end"]                 \* SessionEnded, or StoredTokens of another refresh
              /\ UNCHANGED ver
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, key, start, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

CanRotate(r) == LiveRt(start[r]) /\ idp = "live" /\ nextRt <= MaxTokens
Rotate(r) == [Issue EXCEPT ![start[r]] = "used"]

\* RefreshLock.cs:204 tokenRetriever: RefreshAccessTokenAsync at the IdP. The IdP rotates the refresh token.
ReqIdpRotates(r) ==
    /\ pc[r] = "calling" /\ CanRotate(r)
    /\ rt' = Rotate(r) /\ nextRt' = nextRt + 1
    /\ newTok' = [newTok EXCEPT ![r] = nextRt]
    /\ tries' = [tries EXCEPT ![r] = 0]
    /\ pc' = [pc EXCEPT ![r] = "write"]
    /\ UNCHANGED <<idp, dbVars, inflight, crashes, lVars, key, start, ver, pend, oVars, bVars, why, refusedWithRows>>

\* The IdP rotates, but the response is lost or RefreshTimeout fires first (bff_refresh_timeout, temporary).
\* The row keeps the used token, and nobody has the new one (spec 3.7, "the response got lost").
ReqIdpRotatesResponseLost(r) ==
    /\ pc[r] = "calling" /\ CanRotate(r)
    /\ rt' = Rotate(r) /\ nextRt' = nextRt + 1
    /\ why' = [why EXCEPT ![nextRt] = "the refresh response was lost"]
    /\ pc' = [pc EXCEPT ![r] = "end"]
    /\ UNCHANGED <<idp, dbVars, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, refusedWithRows>>

\* The IdP rotates, but RefreshIdTokenValidation rejects the ID token (bff_invalid_refresh_id_token, permanent).
ReqIdpRotatesIdTokenRejected(r) ==
    /\ pc[r] = "calling" /\ CanRotate(r)
    /\ rt' = Rotate(r) /\ nextRt' = nextRt + 1
    /\ newTok' = [newTok EXCEPT ![r] = nextRt]
    /\ pc' = [pc EXCEPT ![r] = "rejectRevoke"]
    /\ UNCHANGED <<idp, dbVars, inflight, crashes, lVars, key, start, ver, tries, pend, oVars, bVars, why, refusedWithRows>>

\* invalid_grant: the token was used, revoked, or its IdP session ended (permanent).
ReqIdpRejects(r) ==
    /\ pc[r] = "calling" /\ ~(LiveRt(start[r]) /\ idp = "live")
    /\ pc' = [pc EXCEPT ![r] = "permdelete"]
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* A network error, a 5xx, or RefreshTimeout before the IdP acts (temporary).
ReqIdpUnreachable(r) ==
    /\ pc[r] = "calling"
    /\ pc' = [pc EXCEPT ![r] = "end"]
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* RefreshIdTokenValidation revokes the token of the rejected response.
ReqRevokesRejected(r) ==
    /\ pc[r] = "rejectRevoke"
    /\ \/ rt' = Revoke(newTok[r]) /\ UNCHANGED why
       \/ why' = Excuse(newTok[r], "a revocation failed") /\ UNCHANGED rt
    /\ newTok' = [newTok EXCEPT ![r] = 0]
    /\ pc' = [pc EXCEPT ![r] = "permdelete"]
    /\ UNCHANGED <<idp, nextRt, dbVars, inflight, crashes, lVars, key, start, ver, tries, pend, oVars, bVars, refusedWithRows>>

\* RefreshLock.cs:206-208: a permanent failure deletes the row inside the lock transaction.
ReqDeletesRow(r) ==
    /\ pc[r] = "permdelete" /\ rowLock[key[r]] = 0
    /\ IF row[key[r]].exists
         THEN /\ rowLock' = [rowLock EXCEPT ![key[r]] = r]
              /\ pend' = [pend EXCEPT ![r] = "delete"]
              /\ pc' = [pc EXCEPT ![r] = "commit"]
         ELSE /\ pc' = [pc EXCEPT ![r] = "end"] /\ UNCHANGED <<rowLock, pend>>
    /\ UNCHANGED <<idpVars, row, lock, jtiUsed, inflight, crashes, lVars, key, start, ver, newTok, tries, oVars, bVars, why, refusedWithRows>>

\* BffTicketStore.RenewTokensAsync, inside the lock transaction: UPDATE ... WHERE key AND xmin = ver.
\* It waits for the row lock of another transaction. Written, Missing (revoke), or Conflict.
ReqWritesTokens(r) ==
    /\ pc[r] = "write" /\ rowLock[key[r]] = 0
    /\ LET k == key[r] IN
       IF row[k].exists /\ row[k].ver = ver[r]
         THEN /\ rowLock' = [rowLock EXCEPT ![k] = r]
              /\ pend' = [pend EXCEPT ![r] = "write"]
              /\ pc' = [pc EXCEPT ![r] = "commit"]
              /\ UNCHANGED <<ver, tries>>
       ELSE IF ~Readable(k)
         THEN /\ pc' = [pc EXCEPT ![r] = "revokeNew"]          \* BffTicketStore.cs:182 Missing
              /\ UNCHANGED <<rowLock, pend, ver, tries>>
       ELSE IF row[k].rt # start[r]
         THEN /\ pc' = [pc EXCEPT ![r] = "fault"]              \* BffTicketStore.cs:198 a conflict with other tokens throws
              /\ UNCHANGED <<rowLock, pend, ver, tries>>
       ELSE IF tries[r] + 1 < WriteAttempts                    \* a conflict with an expiry renewal: try again
         THEN /\ ver' = [ver EXCEPT ![r] = row[k].ver]
              /\ tries' = [tries EXCEPT ![r] = tries[r] + 1]
              /\ UNCHANGED <<rowLock, pend, pc>>
         ELSE /\ pc' = [pc EXCEPT ![r] = "revokeNew"]          \* BffTicketStore.cs:208 dropped after 3 conflicts
              /\ UNCHANGED <<rowLock, pend, ver, tries>>
    /\ UNCHANGED <<idpVars, row, lock, jtiUsed, inflight, crashes, lVars, key, start, newTok, oVars, bVars, why, refusedWithRows>>

\* BffTicketStore.RevokeNewRefreshTokenAsync: no row holds the new token.
ReqRevokesNew(r) ==
    /\ pc[r] = "revokeNew"
    /\ \/ rt' = Revoke(newTok[r]) /\ UNCHANGED why
       \/ why' = Excuse(newTok[r], "a revocation failed") /\ UNCHANGED rt
    /\ newTok' = [newTok EXCEPT ![r] = 0]
    /\ pc' = [pc EXCEPT ![r] = "end"]
    /\ UNCHANGED <<idp, nextRt, dbVars, inflight, crashes, lVars, key, start, ver, tries, pend, oVars, bVars, refusedWithRows>>

\* RefreshLock.cs:210 CommitAsync. The row change becomes visible, and both locks go.
\* Then the finally of ExecuteWithConcurrencyControlAsync removes the _inFlight entry.
ReqCommits(r) ==
    /\ pc[r] = "commit"
    /\ LET k == key[r] IN
         row' = [row EXCEPT ![k] = IF pend[r] = "write"
                                     THEN [@ EXCEPT !.rt = newTok[r], !.ver = @ + 1]
                                     ELSE [@ EXCEPT !.exists = FALSE]]
    /\ rowLock' = [rowLock EXCEPT ![key[r]] = 0]
    /\ lock' = [lock EXCEPT ![key[r]] = 0]
    /\ inflight' = [inflight EXCEPT ![Inst(r)][start[r]] = IF @ = r THEN 0 ELSE @]
    /\ newTok' = [newTok EXCEPT ![r] = 0]
    /\ pend' = [pend EXCEPT ![r] = "none"]
    /\ pc' = [pc EXCEPT ![r] = "done"]
    /\ UNCHANGED <<idpVars, jtiUsed, crashes, lVars, key, start, ver, tries, oVars, bVars, why, refusedWithRows>>

\* The end of a lock transaction with no row change: commit or rollback, then the finally.
ReqEnds(r) ==
    /\ pc[r] = "end"
    /\ lock' = [lock EXCEPT ![key[r]] = IF @ = r THEN 0 ELSE @]
    /\ inflight' = [inflight EXCEPT ![Inst(r)][start[r]] = IF @ = r THEN 0 ELSE @]
    /\ pc' = [pc EXCEPT ![r] = "done"]
    /\ UNCHANGED <<idpVars, row, rowLock, jtiUsed, crashes, lVars, key, start, ver, newTok, tries, pend, oVars, bVars, why, refusedWithRows>>

\* A database failure after the IdP call: the transaction rolls back, and the execution strategy returns
\* RetryAfterRefresh (RefreshLock.cs:181). The rotated token is in no row (spec 3.7, Cancellation). A live
\* instance that passes the idle limit of the lock transaction gets 25P03, which bffdb retries the same way
\* (BffSessionExtensions.AddBffSessions).
ReqDbFailsAfterCall(r) ==
    /\ pc[r] \in {"write", "commit"}
    /\ why' = Excuse(newTok[r], "the lock transaction ended before its commit")
    /\ rowLock' = [rowLock EXCEPT ![key[r]] = IF @ = r THEN 0 ELSE @]
    /\ lock' = [lock EXCEPT ![key[r]] = 0]
    /\ inflight' = [inflight EXCEPT ![Inst(r)][start[r]] = IF @ = r THEN 0 ELSE @]
    /\ newTok' = [newTok EXCEPT ![r] = 0]
    /\ pend' = [pend EXCEPT ![r] = "none"]
    /\ pc' = [pc EXCEPT ![r] = "done"]
    /\ UNCHANGED <<idpVars, row, jtiUsed, crashes, lVars, key, start, ver, tries, oVars, bVars, refusedWithRows>>

\* BffTicketStore.RenewExpiryAsync, in autocommit and with no lock: UPDATE ... WHERE key AND xmin = ver.
ReqRenewsExpiry(r) ==
    /\ pc[r] = "renew" /\ rowLock[key[r]] = 0
    /\ LET k == key[r] IN
       IF row[k].exists /\ row[k].ver = ver[r]
         THEN /\ row' = [row EXCEPT ![k].ver = @ + 1]
              /\ pc' = [pc EXCEPT ![r] = "done"]
              /\ UNCHANGED <<ver, tries>>
       ELSE IF Readable(k) /\ tries[r] + 1 < WriteAttempts
         THEN /\ ver' = [ver EXCEPT ![r] = row[k].ver]
              /\ tries' = [tries EXCEPT ![r] = tries[r] + 1]
              /\ UNCHANGED <<row, pc>>
         ELSE /\ pc' = [pc EXCEPT ![r] = "done"]
              /\ UNCHANGED <<row, ver, tries>>
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, lVars, key, start, newTok, pend, oVars, bVars, why, refusedWithRows>>

-----------------------------------------------------------------------------
(* /bff/logout: LogoutEndpoint.HandleAsync *)

\* LogoutEndpoint.cs:36: authenticate the cookie.
LogoutReads ==
    /\ WithLogout /\ opc = "idle"
    /\ IF Readable(cookie) THEN opc' = "auth" /\ okey' = cookie ELSE opc' = "done" /\ UNCHANGED okey
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, rVars, otok, loggedOut, bVars, why, refusedWithRows>>

\* LogoutEndpoint.cs:59: DELETE ... RETURNING. It waits for the row lock of a refresh transaction.
LogoutDeletes ==
    /\ opc = "auth" /\ rowLock[okey] = 0
    /\ IF row[okey].exists
         THEN /\ row' = [row EXCEPT ![okey].exists = FALSE]
              /\ otok' = row[okey].rt
              /\ opc' = "revoke"
         ELSE /\ opc' = "signout" /\ UNCHANGED <<row, otok>>
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, lVars, rVars, okey, loggedOut, bVars, why, refusedWithRows>>

\* LogoutEndpoint.cs:64: revoke the token of the deleted row. An IdP error is logged, and the logout goes on.
LogoutRevokes ==
    /\ opc = "revoke"
    /\ \/ rt' = Revoke(otok) /\ UNCHANGED why
       \/ why' = Excuse(otok, "a revocation failed") /\ UNCHANGED rt
    /\ otok' = 0 /\ opc' = "signout"
    /\ UNCHANGED <<idp, nextRt, dbVars, inflight, crashes, lVars, rVars, okey, loggedOut, bVars, refusedWithRows>>

\* LogoutEndpoint.cs:67: sign out the cookie. The logout completed.
LogoutSignsOut ==
    /\ opc = "signout"
    /\ cookie' = IF cookie = okey THEN 0 ELSE cookie
    /\ loggedOut' = okey /\ opc' = "done"
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lpc, ltok, logins, rVars, okey, otok, bVars, why, refusedWithRows>>

-----------------------------------------------------------------------------
(* /bff/backchannel: BackChannelLogoutEndpoint.HandleAsync *)

\* The IdP sends the logout token of S, again after a crash, a 400, or a 500, until it gets a 200.
BcDelivered ==
    /\ bpc = "idle" /\ idp = "ended" /\ ~bcOk /\ deliveries < MaxDeliveries
    /\ deliveries' = deliveries + 1 /\ bpc' = "insert"
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, rVars, oVars, found, bcDel, jtiPend, bcOk, why, refusedWithRows>>

\* BackChannelLogoutEndpoint.cs:86 replayCache.TryAddAsync: INSERT ... ON CONFLICT DO NOTHING, in the logout
\* transaction. The jti commits with the deletes (BcAnswers200).
BcInsertsJti ==
    /\ bpc = "insert"
    /\ IF jtiUsed
         THEN /\ refusedWithRows' = (refusedWithRows \/ \E k \in Keys : Readable(k))   \* 400, a replay
              /\ bpc' = "idle"
              /\ UNCHANGED jtiPend
         ELSE /\ jtiPend' = TRUE
              /\ bpc' = "find"
              /\ UNCHANGED refusedWithRows
    /\ UNCHANGED <<idpVars, row, rowLock, lock, jtiUsed, inflight, crashes, lVars, rVars, oVars, found, bcDel, deliveries, bcOk, why>>

\* The insert fails: 400, and no session ends (BackChannelLogoutEndpoint.cs:97).
BcInsertFails ==
    /\ bpc = "insert"
    /\ bpc' = "idle"
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, rVars, oVars, found, bcDel, jtiPend, deliveries, bcOk, why, refusedWithRows>>

\* BackChannelLogoutEndpoint.cs:179 FindBySessionIdAsync (or FindBySubjectAsync): the readable rows of S.
BcFindsRows ==
    /\ bpc = "find"
    /\ found' = {k \in Keys : Readable(k)}
    /\ bpc' = "delete"
    /\ UNCHANGED <<idpVars, dbVars, inflight, crashes, lVars, rVars, oVars, bcDel, jtiPend, deliveries, bcOk, why, refusedWithRows>>

\* BackChannelLogoutEndpoint.cs:189 DeleteAsync of one found row, in the logout transaction. It waits for the row
\* lock of a refresh, and it holds the row lock until the commit.
BcDeletesRow ==
    /\ bpc = "delete"
    /\ \E k \in found :
         /\ rowLock[k] = 0
         /\ found' = found \ {k}
         /\ IF row[k].exists THEN rowLock' = [rowLock EXCEPT ![k] = BC] /\ bcDel' = bcDel \cup {k}
                              ELSE UNCHANGED <<rowLock, bcDel>>
    /\ UNCHANGED <<idpVars, row, lock, jtiUsed, inflight, crashes, lVars, rVars, oVars, bpc, jtiPend, deliveries, bcOk, why, refusedWithRows>>

ReleaseBc == [k \in Keys |-> IF rowLock[k] = BC THEN 0 ELSE rowLock[k]]

\* A row delete throws, after the retries of the execution strategy: a rollback, then 500 (BackChannelLogoutEndpoint.cs:104).
\* No row ends, and the jti goes with the rollback, so the IdP can send the token again.
BcDeleteFails ==
    /\ bpc = "delete" /\ found # {}
    /\ rowLock' = ReleaseBc /\ bcDel' = {} /\ jtiPend' = FALSE /\ found' = {}
    /\ bpc' = "idle"
    /\ UNCHANGED <<idpVars, row, lock, jtiUsed, inflight, crashes, lVars, rVars, oVars, deliveries, bcOk, why, refusedWithRows>>

\* All found rows are gone: the jti and the deletes commit, then 200. If the answer to the commit gets lost,
\* verifySucceeded of ExecuteInTransactionAsync asks Postgres if this transaction committed, and the answer is
\* 200 too (BackChannelLogoutEndpoint.cs:94).
BcAnswers200 ==
    /\ bpc = "delete" /\ found = {}
    /\ row' = [k \in Keys |-> IF k \in bcDel THEN [row[k] EXCEPT !.exists = FALSE] ELSE row[k]]
    /\ rowLock' = ReleaseBc /\ bcDel' = {}
    /\ jtiUsed' = (jtiUsed \/ jtiPend) /\ jtiPend' = FALSE
    /\ bcOk' = TRUE /\ bpc' = "idle"
    /\ UNCHANGED <<idpVars, lock, inflight, crashes, lVars, rVars, oVars, found, deliveries, why, refusedWithRows>>

-----------------------------------------------------------------------------
(* Expiry and BffSessionCleanupService *)

RowExpires(k) ==
    /\ WithExpiry /\ row[k].exists /\ ~row[k].expired
    /\ row' = [row EXCEPT ![k].expired = TRUE]
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, lVars, rVars, oVars, bVars, why, refusedWithRows>>

\* EfBffSessionStore.DeleteEndedAsync. It does not revoke the refresh token.
CleanupDeletes(k) ==
    /\ row[k].exists /\ row[k].expired /\ rowLock[k] = 0
    /\ row' = [row EXCEPT ![k].exists = FALSE]
    /\ why' = Excuse(row[k].rt, "the cleanup deleted an expired row")
    /\ UNCHANGED <<idpVars, rowLock, lock, jtiUsed, inflight, crashes, lVars, rVars, oVars, bVars, refusedWithRows>>

-----------------------------------------------------------------------------
(* A crash of instance i: its requests stop, Postgres rolls back their transactions and frees their locks,
   and _inFlight starts empty. Requests that did not start yet run after the restart. An instance that stops
   with its connection open is the same crash: the refresh lock transaction (RefreshLock.TryLockAsync) and the
   logout transaction (BackChannelLogoutEndpoint.LimitIdleTimeAsync) set idle_in_transaction_session_timeout,
   so Postgres ends its session at the limit. *)

Dies(r, i) == Inst(r) = i /\ pc[r] \notin {"idle", "done"}

Crash(i) ==
    /\ crashes < MaxCrashes
    /\ crashes' = crashes + 1
    /\ inflight' = [inflight EXCEPT ![i] = [t \in Tokens |-> 0]]
    /\ pc' = [r \in Reqs |-> IF Dies(r, i) THEN "done" ELSE pc[r]]
    /\ newTok' = [r \in Reqs |-> IF Dies(r, i) THEN 0 ELSE newTok[r]]
    /\ pend' = [r \in Reqs |-> IF Dies(r, i) THEN "none" ELSE pend[r]]
    /\ lock' = [k \in Keys |-> IF lock[k] # 0 /\ Inst(lock[k]) = i THEN 0 ELSE lock[k]]
    /\ rowLock' = [k \in Keys |-> IF \/ (rowLock[k] \in Reqs /\ Inst(rowLock[k]) = i)
                                    \/ (rowLock[k] = BC /\ i = BcInst)
                                 THEN 0 ELSE rowLock[k]]
    \* The tokens that only a dying step held: a rotated token before its commit, the token of a login before
    \* its row, and the token of a deleted row before its revocation.
    /\ LET lost == {newTok[r] : r \in {q \in Reqs : Dies(q, i)}}
                   \cup (IF i = LoginInst THEN {ltok} ELSE {})
                   \cup (IF i = LogoutInst /\ opc = "revoke" THEN {otok} ELSE {})
       IN why' = [t \in Tokens |-> IF t \in lost /\ LiveRt(t) /\ why[t] = "none"
                                   THEN "a crash before the row write or the revocation" ELSE why[t]]
    /\ lpc' = IF i = LoginInst THEN "start" ELSE lpc
    /\ ltok' = IF i = LoginInst THEN 0 ELSE ltok
    /\ opc' = IF i = LogoutInst /\ opc \notin {"idle", "done"} THEN "done" ELSE opc
    /\ otok' = IF i = LogoutInst THEN 0 ELSE otok
    /\ bpc' = IF i = BcInst THEN "idle" ELSE bpc
    /\ found' = IF i = BcInst THEN {} ELSE found
    /\ bcDel' = IF i = BcInst THEN {} ELSE bcDel
    /\ jtiPend' = IF i = BcInst THEN FALSE ELSE jtiPend
    /\ UNCHANGED <<idpVars, row, jtiUsed, cookie, logins, key, start, ver, tries, okey, loggedOut, deliveries, bcOk, refusedWithRows>>

-----------------------------------------------------------------------------

\* No step is left: every process is at its end. A stop anywhere else is a deadlock.
Finished ==
    /\ \A r \in Reqs : pc[r] \in {"done", "fault"}
    /\ lpc = "start" /\ (logins = Logins \/ idp = "ended" \/ nextRt > MaxTokens)
    /\ (opc = "done" \/ ~WithLogout)
    /\ idp # "live"
    /\ bpc = "idle" /\ (bcOk \/ deliveries = MaxDeliveries)
    /\ \A k \in Keys : ~(row[k].exists /\ row[k].expired)
    /\ \A k \in Keys : ~(WithExpiry /\ row[k].exists /\ ~row[k].expired)
    /\ UNCHANGED vars

ReqNext(r) ==
    \/ ReqReads(r) \/ ReqJoinsInFlight(r) \/ ReqWaiterEnds(r) \/ ReqTakesLock(r) \/ ReqLockTimesOut(r)
    \/ ReqRereads(r) \/ ReqIdpRotates(r) \/ ReqIdpRotatesResponseLost(r) \/ ReqIdpRotatesIdTokenRejected(r)
    \/ ReqIdpRejects(r) \/ ReqIdpUnreachable(r) \/ ReqRevokesRejected(r) \/ ReqDeletesRow(r)
    \/ ReqWritesTokens(r) \/ ReqRevokesNew(r) \/ ReqCommits(r) \/ ReqEnds(r) \/ ReqDbFailsAfterCall(r)
    \/ ReqRenewsExpiry(r)

LoginNext == LoginAuthorize \/ LoginExchange \/ LoginExchangeFails \/ LoginEndsOldSession \/ LoginCreatesRow
LogoutNext == LogoutReads \/ LogoutDeletes \/ LogoutRevokes \/ LogoutSignsOut
BcNext == BcDelivered \/ BcInsertsJti \/ BcInsertFails \/ BcFindsRows \/ BcDeletesRow \/ BcDeleteFails
          \/ BcAnswers200

Next ==
    \/ IdpEndsSession
    \/ LoginNext
    \/ \E r \in Reqs : ReqNext(r)
    \/ LogoutNext
    \/ BcNext
    \/ \E k \in Keys : RowExpires(k) \/ CleanupDeletes(k)
    \/ \E i \in {1, 2} : Crash(i)
    \/ Finished

\* Each request and each back-channel handling keeps moving while it can. Crashes, expiry, the IdP, and the
\* user need no fairness.
Fairness ==
    /\ \A r \in Reqs : WF_vars(ReqNext(r))
    /\ WF_vars(BcInsertsJti \/ BcInsertFails \/ BcFindsRows \/ BcDeletesRow \/ BcDeleteFails \/ BcAnswers200)
    /\ WF_vars(LogoutDeletes \/ LogoutRevokes \/ LogoutSignsOut)

Spec == Init /\ [][Next]_vars /\ Fairness

-----------------------------------------------------------------------------
(* The rules *)

Held(t) ==
    \/ \E k \in Keys : row[k].exists /\ row[k].rt = t
    \/ \E r \in Reqs : newTok[r] = t
    \/ ltok = t
    \/ (opc = "revoke" /\ otok = t)

\* The IdP never holds a live refresh token that no row and no running step holds, unless one of the allowed
\* windows left it there (why[t] names the window).
NoUnexcusedOrphanToken == \A t \in Tokens : (LiveRt(t) /\ ~Held(t)) => why[t] # "none"

\* After /bff/logout completes, no readable row has its key.
LogoutEndsSession == loggedOut # 0 => ~Readable(loggedOut)

\* After /bff/backchannel answers 200 for S, no readable row has the sid of S.
BackChannelLogoutEndsSessions == bcOk => \A k \in Keys : ~Readable(k)

\* The BFF refuses a logout token as a replay only when no readable row of its sid is left.
ReplayRefusedOnlyWhenEnded == ~refusedWithRows

\* A token write inside the lock never meets other tokens (BffTicketStore.cs:198 throws).
NoTokenWriteConflict == \A r \in Reqs : pc[r] # "fault"

LeaderPhases == {"lockwait", "locked", "calling", "write", "revokeNew", "rejectRevoke", "permdelete", "commit", "end"}

\* An _inFlight entry belongs to a running leader with that token, so it never blocks a later refresh.
NoStaleInFlightEntry ==
    \A i \in {1, 2}, t \in Tokens :
        inflight[i][t] # 0 => (pc[inflight[i][t]] \in LeaderPhases /\ start[inflight[i][t]] = t)

\* The advisory lock and the row locks belong to a running transaction.
NoStaleLock ==
    /\ \A k \in Keys : lock[k] # 0 => pc[lock[k]] \in LeaderPhases \ {"lockwait"}
    /\ \A k \in Keys : rowLock[k] \in Reqs => pc[rowLock[k]] = "commit"
    /\ \A k \in Keys : rowLock[k] = BC => bpc = "delete"

\* Every refresh in flight ends: a success, a temporary failure, a permanent failure, or the crash of its instance.
RefreshEnds == \A r \in Reqs : (pc[r] \notin {"idle", "done"}) ~> (pc[r] = "done")

\* Every back-channel handling ends with an answer, or with the crash of its instance.
BackChannelEnds == (bpc # "idle") ~> (bpc = "idle")

\* Symmetry is not used: the requests run on different instances.
=============================================================================
