---------------------------- MODULE Membership ----------------------------
(***************************************************************************)
(* Tenant membership: the invite lifecycle (Pending, Claimed, Revoked, and *)
(* expired as computed), user provisioning, the role change and the delete *)
(* of a user, and tenant delete.                                           *)
(*                                                                         *)
(* Each action is one database transaction or one step of a request that  *)
(* the code can be paused at. The comment on each action names its code.  *)
(* Paths are from the repo root. See ../SKILL.md for the coverage record. *)
(***************************************************************************)
EXTENDS Naturals, Sequences, FiniteSets

CONSTANTS
    Claims,        \* claim requests (ClaimInvite.Command), one sub and one invite each
    SubOf(_),      \* the sub of the caller of a claim
    InvOf(_),      \* the invite whose token the claim sends
    Invs,          \* invites of tenant T1, all Pending at the start
    Revokes,       \* invites that an admin sends one RevokeInvite.Command for
    Initiators,    \* subs that send one InitiateUserAndTenant.Command each
    DeleteTenantReq, \* TRUE: an admin sends one DeleteTenant.Command for T1
    Admins,        \* the subs of the admins of T1 at the start, as a sequence
    Changes,       \* member changes: one UpdateUserRole.Command to User, or one DeleteUser.Command, each
    ChangeSub(_),  \* the sub of the user that the change names
    ChangeKind(_), \* "demote" (UpdateUserRole to User) or "delete" (DeleteUser)
    MaxRuns,       \* RaceRetryBehavior.MaxRuns (20 in the code)
    InviteVersionCheck, \* TRUE in the code. FALSE mirrors a scratch break: no xmin check on an invite update
    ClaimTakesLock, \* TRUE in the code. FALSE mirrors a scratch break: the claim skips RootLock.Tenant.RunSharedAsync
    LastAdminRule, \* TRUE in the code. FALSE mirrors the code before UM-4: no check of the last admin
    AdminTakesLock \* TRUE in the code. FALSE mirrors a scratch break: the change checks UM-4 with no tenant lock

T1 == "t1"
\* A new tenant of InitiateUserAndTenant is named after the sub that starts it.
Tenants == {T1} \cup Initiators
NoInvite == "none"
NoLock == "none"

VARIABLES
    tenantLive,   \* [Tenants -> BOOLEAN]. Tenant rows (soft delete)
    inv,          \* [Invs -> [status, deleted, ver, by]]. Invite rows, ver is xmin
    expired,      \* [Invs -> BOOLEAN]. The clock passed ExpiresAt
    users,        \* Seq of [sub, tenant, from, live, ver, admin]. User rows (soft delete), admin is the role
    moves,        \* ghost: [Invs -> Nat], the status changes of each invite
    cpc, cruns, csnap, cres, \* claim requests: step, runs, loaded version, result
    rpc, rruns, rsnap, rres, \* revoke requests
    dpc, druns, dsnap, dres, \* the tenant delete request
    ipc, ires,    \* InitiateUserAndTenant requests
    mpc, mruns, mk, msnap, mres, \* member changes: step, runs, the user found, loaded version, result
    shared,       \* the claims that hold the shared lock of T1
    excl,         \* [Tenants -> NoLock or the request that holds the exclusive lock of the tenant]
    revokedUnder  \* ghost: [Claims -> BOOLEAN], a revoke committed while the claim was in flight

ghostVars == <<moves, revokedUnder>>
changeVars == <<mpc, mruns, mk, msnap, mres>>
reqVars == <<cpc, cruns, csnap, cres, rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, ipc, ires, changeVars>>
vars == <<tenantLive, inv, expired, users, shared, excl, reqVars, ghostVars>>

DeleteReqs == IF DeleteTenantReq THEN {"d"} ELSE {}

-----------------------------------------------------------------------------
\* Reads that the code makes.

UserIds == 1..Len(users)
HasLiveUser(s) == \E k \in UserIds : users[k].live /\ users[k].sub = s
LiveT1Users == {k \in UserIds : users[k].live /\ users[k].tenant = T1}
Claimable(i) == inv[i].status = "Pending" /\ ~expired[i]   \* Invite.GuardClaimable
LiveUsersOf(s) == {k \in UserIds : users[k].live /\ users[k].sub = s}
\* "users.load_admins" (AdminCaseLoader): the live admins of the tenant, less the user.
OtherAdmins(t, k) == {m \in UserIds : m # k /\ users[m].live /\ users[m].tenant = t /\ users[m].admin}
Release(r) == [t \in Tenants |-> IF excl[t] = r THEN NoLock ELSE excl[t]]

Init ==
    /\ tenantLive = [t \in Tenants |-> t = T1]
    /\ inv = [i \in Invs |-> [status |-> "Pending", deleted |-> FALSE, ver |-> 0, by |-> 0]]
    /\ expired = [i \in Invs |-> FALSE]
    /\ users = [k \in 1..Len(Admins) |->
                  [sub |-> Admins[k], tenant |-> T1, from |-> NoInvite, live |-> TRUE, ver |-> 0, admin |-> TRUE]]
    /\ moves = [i \in Invs |-> 0]
    /\ cpc = [c \in Claims |-> "idle"] /\ cruns = [c \in Claims |-> 1]
    /\ csnap = [c \in Claims |-> 0] /\ cres = [c \in Claims |-> "none"]
    /\ rpc = [i \in Revokes |-> "idle"] /\ rruns = [i \in Revokes |-> 1]
    /\ rsnap = [i \in Revokes |-> 0] /\ rres = [i \in Revokes |-> "none"]
    /\ dpc = [d \in DeleteReqs |-> "idle"] /\ druns = [d \in DeleteReqs |-> 1]
    /\ dsnap = [d \in DeleteReqs |-> [invs |-> [i \in Invs |-> 0], users |-> {}, uver |-> <<>>]]
    /\ dres = [d \in DeleteReqs |-> "none"]
    /\ ipc = [s \in Initiators |-> "idle"] /\ ires = [s \in Initiators |-> "none"]
    /\ mpc = [r \in Changes |-> "idle"] /\ mruns = [r \in Changes |-> 1] /\ mk = [r \in Changes |-> 0]
    /\ msnap = [r \in Changes |-> 0] /\ mres = [r \in Changes |-> "none"]
    /\ shared = {}
    /\ excl = [t \in Tenants |-> NoLock]
    /\ revokedUnder = [c \in Claims |-> FALSE]

-----------------------------------------------------------------------------
\* ClaimInvite (Domain/Invites/Features/ClaimInvite/ClaimInvite.cs)

ClaimEnd(c, r) ==
    /\ cpc' = [cpc EXCEPT ![c] = "done"] /\ cres' = [cres EXCEPT ![c] = r]
    /\ shared' = shared \ {c}

\* The pre-check of a live user (INV-6), then the token lookup with no lock ("invites.claim.find_tenant").
\* The lookup skips the tenant filter, not the soft delete filter, so a deleted invite gives 404.
ClaimFindTenant(c) ==
    /\ cpc[c] = "idle"
    /\ IF HasLiveUser(SubOf(c)) THEN ClaimEnd(c, "409")
       ELSE IF inv[InvOf(c)].deleted THEN ClaimEnd(c, "404")
       ELSE /\ cpc' = [cpc EXCEPT ![c] = "found"] /\ UNCHANGED <<cres, shared>>
    /\ UNCHANGED <<tenantLive, inv, expired, users, excl, cruns, csnap, rpc, rruns, rsnap, rres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, ghostVars>>

\* Take the shared lock of the tenant, load the invite ("invites.claim.load"), and run Invite.Claim in memory.
ClaimLoad(c) ==
    LET i == InvOf(c) IN
    /\ cpc[c] = "found"
    /\ ClaimTakesLock => excl[T1] = NoLock
    /\ IF inv[i].deleted THEN ClaimEnd(c, "404") /\ UNCHANGED csnap
       ELSE IF ~Claimable(i) THEN ClaimEnd(c, "410") /\ UNCHANGED csnap
       ELSE /\ cpc' = [cpc EXCEPT ![c] = "loaded"]
            /\ csnap' = [csnap EXCEPT ![c] = inv[i].ver]
            /\ shared' = IF ClaimTakesLock THEN shared \cup {c} ELSE shared
            /\ UNCHANGED cres
    /\ UNCHANGED <<tenantLive, inv, expired, users, excl, cruns, rpc, rruns, rsnap, rres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, ghostVars>>

\* One save inserts the user and updates the invite with the xmin check.
\* The unique index ix_users_identifier (live users of all tenants) stops a second user of one sub.
\* When both the index and the version check fail, either error can come first.
ClaimSave(c) ==
    LET i == InvOf(c)
        stale == InviteVersionCheck /\ inv[i].ver # csnap[c]
        dup == HasLiveUser(SubOf(c))
    IN
    /\ cpc[c] = "loaded"
    /\ \/ /\ ~stale /\ ~dup
          /\ users' = Append(users, [sub |-> SubOf(c), tenant |-> T1, from |-> i, live |-> TRUE, ver |-> 0, admin |-> FALSE])
          /\ inv' = [inv EXCEPT ![i].status = "Claimed", ![i].by = Len(users) + 1, ![i].ver = @ + 1]
          /\ moves' = [moves EXCEPT ![i] = IF inv[i].status # "Claimed" THEN @ + 1 ELSE @]
          /\ ClaimEnd(c, "ok")
          /\ UNCHANGED cruns
       \/ /\ dup
          /\ ClaimEnd(c, "409")
          /\ UNCHANGED <<users, inv, moves, cruns>>
       \* StaleVersionException: RaceRetryBehavior runs the handler again from the token lookup.
       \/ /\ stale
          /\ IF cruns[c] < MaxRuns
             THEN /\ cpc' = [cpc EXCEPT ![c] = "idle"] /\ cruns' = [cruns EXCEPT ![c] = @ + 1]
                  /\ shared' = shared \ {c} /\ UNCHANGED cres
             ELSE /\ ClaimEnd(c, "409-concurrent") /\ UNCHANGED cruns
          /\ UNCHANGED <<users, inv, moves>>
    /\ UNCHANGED <<tenantLive, expired, excl, csnap, rpc, rruns, rsnap, rres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, revokedUnder>>

\* A crash, a cancel, or a timeout: the transaction rolls back, and the session lock ends with its connection.
ClaimFails(c) ==
    /\ cpc[c] \in {"found", "loaded"}
    /\ ClaimEnd(c, "failed")
    /\ UNCHANGED <<tenantLive, inv, expired, users, excl, cruns, csnap, rpc, rruns, rsnap, rres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, ghostVars>>

-----------------------------------------------------------------------------
\* RevokeInvite (Domain/Invites/Features/RevokeInvite/RevokeInvite.cs)

RevokeEnd(i, r) == rpc' = [rpc EXCEPT ![i] = "done"] /\ rres' = [rres EXCEPT ![i] = r]

\* Load ("invites.revoke.load") and run Invite.Revoke in memory.
RevokeLoad(i) ==
    /\ rpc[i] = "idle"
    /\ IF inv[i].deleted THEN RevokeEnd(i, "404") /\ UNCHANGED rsnap
       ELSE IF inv[i].status = "Claimed" THEN RevokeEnd(i, "400") /\ UNCHANGED rsnap \* InviteStatus.MoveTo refuses
       ELSE IF inv[i].status = "Revoked" THEN RevokeEnd(i, "ok") /\ UNCHANGED rsnap  \* no change, no write
       ELSE /\ rpc' = [rpc EXCEPT ![i] = "loaded"] /\ rsnap' = [rsnap EXCEPT ![i] = inv[i].ver]
            /\ UNCHANGED rres
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, excl, rruns, cpc, cruns, csnap, cres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, ghostVars>>

\* The save updates the invite with the xmin check. A revoke takes no lock.
RevokeSave(i) ==
    /\ rpc[i] = "loaded"
    /\ IF InviteVersionCheck /\ inv[i].ver # rsnap[i]
       THEN /\ IF rruns[i] < MaxRuns
               THEN rpc' = [rpc EXCEPT ![i] = "idle"] /\ rruns' = [rruns EXCEPT ![i] = @ + 1] /\ UNCHANGED rres
               ELSE RevokeEnd(i, "409-concurrent") /\ UNCHANGED rruns
            /\ UNCHANGED <<inv, moves, revokedUnder>>
       ELSE /\ inv' = [inv EXCEPT ![i].status = "Revoked", ![i].ver = @ + 1]
            /\ moves' = [moves EXCEPT ![i] = IF inv[i].status # "Revoked" THEN @ + 1 ELSE @]
            /\ revokedUnder' = [c \in Claims |->
                   revokedUnder[c] \/ (InvOf(c) = i /\ cpc[c] \in {"found", "loaded"})]
            /\ RevokeEnd(i, "ok")
            /\ UNCHANGED rruns
    /\ UNCHANGED <<tenantLive, expired, users, shared, excl, rsnap, cpc, cruns, csnap, cres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars>>

RevokeFails(i) ==
    /\ rpc[i] = "loaded"
    /\ RevokeEnd(i, "failed")
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, excl, rruns, rsnap, cpc, cruns, csnap, cres,
                   dpc, druns, dsnap, dres, ipc, ires, changeVars, ghostVars>>

-----------------------------------------------------------------------------
\* DeleteTenant (Domain/Tenants/Features/DeleteTenant/DeleteTenant.cs)

DeleteEnd(d, r) == dpc' = [dpc EXCEPT ![d] = "done"] /\ dres' = [dres EXCEPT ![d] = r] /\ excl' = Release(d)

\* Take the exclusive lock of T1, then load the tenant and its owned rows (TenantOwnedRows.LoadAsync).
DeleteLoad(d) ==
    /\ dpc[d] = "idle"
    /\ excl[T1] = NoLock /\ shared = {}
    /\ IF ~tenantLive[T1] THEN DeleteEnd(d, "404") /\ UNCHANGED dsnap
       ELSE /\ excl' = [excl EXCEPT ![T1] = d] /\ dpc' = [dpc EXCEPT ![d] = "loaded"]
            /\ dsnap' = [dsnap EXCEPT ![d] = [invs |-> [i \in Invs |-> inv[i].ver],
                                              users |-> LiveT1Users,
                                              uver |-> [k \in UserIds |-> users[k].ver]]]
            /\ UNCHANGED dres
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, druns, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, ipc, ires, changeVars, ghostVars>>

\* One save soft-deletes the loaded rows and the tenant. Each loaded row has the xmin check.
\* A row that came after the load is not in the save.
DeleteSave(d) ==
    LET s == dsnap[d]
        stale == \/ InviteVersionCheck /\ \E i \in Invs : inv[i].ver # s.invs[i]
                 \/ \E k \in s.users : users[k].ver # s.uver[k]
    IN
    /\ dpc[d] = "loaded"
    /\ IF stale
       THEN IF druns[d] < MaxRuns
            THEN /\ dpc' = [dpc EXCEPT ![d] = "idle"] /\ druns' = [druns EXCEPT ![d] = @ + 1]
                 /\ excl' = Release(d) /\ UNCHANGED dres
            ELSE DeleteEnd(d, "409-concurrent") /\ UNCHANGED druns
       ELSE /\ tenantLive' = [tenantLive EXCEPT ![T1] = FALSE]
            /\ inv' = [i \in Invs |-> IF inv[i].deleted THEN inv[i]
                                      ELSE [inv[i] EXCEPT !.deleted = TRUE, !.ver = @ + 1]]
            /\ users' = [k \in UserIds |-> IF k \in s.users
                                           THEN [users[k] EXCEPT !.live = FALSE, !.ver = @ + 1]
                                           ELSE users[k]]
            /\ DeleteEnd(d, "ok")
            /\ UNCHANGED druns
    /\ (stale => UNCHANGED <<tenantLive, inv, users>>)
    /\ UNCHANGED <<expired, shared, dsnap, cpc, cruns, csnap, cres, rpc, rruns, rsnap, rres,
                   ipc, ires, changeVars, ghostVars>>

DeleteFails(d) ==
    /\ dpc[d] = "loaded"
    /\ DeleteEnd(d, "failed")
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, druns, dsnap, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, ipc, ires, changeVars, ghostVars>>

-----------------------------------------------------------------------------
\* UpdateUserRole (Domain/Users/Features/UpdateUserRole/UpdateUserRole.cs) to the User role, and
\* DeleteUser (Domain/Users/Features/DeleteUser/DeleteUser.cs). Both run again when raced, and both check UM-4
\* with TenantAdmins.Decide (Domain/Users/TenantAdmins.cs). A soft delete frees the identifier, so the sub can
\* join again.

ChangeEnd(r, res) == mpc' = [mpc EXCEPT ![r] = "done"] /\ mres' = [mres EXCEPT ![r] = res] /\ excl' = Release(r)

\* "users.update_role.find_tenant" or "users.delete.find_tenant", with no lock. The request names the ID of the user
\* that the admin saw, here the live user of the sub.
ChangeFind(r) ==
    /\ mpc[r] = "idle"
    /\ IF LiveUsersOf(ChangeSub(r)) = {} THEN ChangeEnd(r, "404") /\ UNCHANGED mk
       ELSE /\ mk' = [mk EXCEPT ![r] = CHOOSE k \in LiveUsersOf(ChangeSub(r)) : TRUE]
            /\ mpc' = [mpc EXCEPT ![r] = "found"] /\ UNCHANGED <<mres, excl>>
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, mruns, msnap, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, ipc, ires, ghostVars>>

\* RootLock.Tenant.RunExclusiveAsync on the tenant of the user, the load of the user, the load of the admins of
\* the tenant, and TenantAdmins.Decide in memory. A demote of a user who is not an admin changes nothing.
ChangeLoad(r) ==
    LET k == mk[r]
        t == users[k].tenant
    IN
    /\ mpc[r] = "found"
    /\ AdminTakesLock => (excl[t] = NoLock /\ (t = T1 => shared = {}))
    /\ IF ~users[k].live THEN ChangeEnd(r, "404") /\ UNCHANGED msnap
       ELSE IF LastAdminRule /\ users[k].admin /\ OtherAdmins(t, k) = {}
            THEN ChangeEnd(r, "422") /\ UNCHANGED msnap
       ELSE IF ChangeKind(r) = "demote" /\ ~users[k].admin THEN ChangeEnd(r, "ok") /\ UNCHANGED msnap
       ELSE /\ mpc' = [mpc EXCEPT ![r] = "loaded"] /\ msnap' = [msnap EXCEPT ![r] = users[k].ver]
            /\ excl' = IF AdminTakesLock THEN [excl EXCEPT ![t] = r] ELSE excl
            /\ UNCHANGED mres
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, mruns, mk, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, ipc, ires, ghostVars>>

\* The one save updates the user row with the xmin check: the new role and permissions, or the soft delete.
\* On StaleVersionException, RaceRetryBehavior runs the handler again from the lookup.
ChangeSave(r) ==
    LET k == mk[r] IN
    /\ mpc[r] = "loaded"
    /\ IF users[k].ver # msnap[r]
       THEN /\ IF mruns[r] < MaxRuns
               THEN /\ mpc' = [mpc EXCEPT ![r] = "idle"] /\ mruns' = [mruns EXCEPT ![r] = @ + 1]
                    /\ excl' = Release(r) /\ UNCHANGED mres
               ELSE ChangeEnd(r, "409-concurrent") /\ UNCHANGED mruns
            /\ UNCHANGED users
       ELSE /\ users' = IF ChangeKind(r) = "demote"
                        THEN [users EXCEPT ![k].admin = FALSE, ![k].ver = @ + 1]
                        ELSE [users EXCEPT ![k].live = FALSE, ![k].ver = @ + 1]
            /\ ChangeEnd(r, "ok")
            /\ UNCHANGED mruns
    /\ UNCHANGED <<tenantLive, inv, expired, shared, mk, msnap, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, ipc, ires, ghostVars>>

ChangeFails(r) ==
    /\ mpc[r] \in {"found", "loaded"}
    /\ ChangeEnd(r, "failed")
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, mruns, mk, msnap, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, ipc, ires, ghostVars>>

-----------------------------------------------------------------------------
\* InitiateUserAndTenant (Domain/Users/Features/InitiateUserAndTenant/InitiateUserAndTenant.cs)

\* "users.initiate.identifier_in_use", with no lock.
InitiateCheck(s) ==
    /\ ipc[s] = "idle"
    /\ IF HasLiveUser(s)
       THEN ipc' = [ipc EXCEPT ![s] = "done"] /\ ires' = [ires EXCEPT ![s] = "409"]
       ELSE ipc' = [ipc EXCEPT ![s] = "checked"] /\ UNCHANGED ires
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, excl, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, changeVars, ghostVars>>

\* One save inserts the tenant and the user. The unique index stops a second live user of one sub.
InitiateSave(s) ==
    /\ ipc[s] = "checked"
    /\ ipc' = [ipc EXCEPT ![s] = "done"]
    /\ IF HasLiveUser(s)
       THEN ires' = [ires EXCEPT ![s] = "409"] /\ UNCHANGED <<tenantLive, users>>
       ELSE /\ tenantLive' = [tenantLive EXCEPT ![s] = TRUE]
            /\ users' = Append(users, [sub |-> s, tenant |-> s, from |-> NoInvite, live |-> TRUE, ver |-> 0, admin |-> TRUE])
            /\ ires' = [ires EXCEPT ![s] = "ok"]
    /\ UNCHANGED <<inv, expired, shared, excl, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, changeVars, ghostVars>>

InitiateFails(s) ==
    /\ ipc[s] = "checked"
    /\ ipc' = [ipc EXCEPT ![s] = "done"] /\ ires' = [ires EXCEPT ![s] = "failed"]
    /\ UNCHANGED <<tenantLive, inv, expired, users, shared, excl, cpc, cruns, csnap, cres,
                   rpc, rruns, rsnap, rres, dpc, druns, dsnap, dres, changeVars, ghostVars>>

-----------------------------------------------------------------------------
\* The clock passes ExpiresAt. Invite.IsExpiredAt computes expiry, so no process must be alive for it.

ClockPassesExpiry(i) ==
    /\ ~expired[i]
    /\ expired' = [expired EXCEPT ![i] = TRUE]
    /\ UNCHANGED <<tenantLive, inv, users, shared, excl, reqVars, ghostVars>>

AllDone ==
    /\ \A c \in Claims : cpc[c] = "done"
    /\ \A i \in Revokes : rpc[i] = "done"
    /\ \A d \in DeleteReqs : dpc[d] = "done"
    /\ \A s \in Initiators : ipc[s] = "done"
    /\ \A r \in Changes : mpc[r] = "done"

\* Every request has an answer and the clock has passed each expiry: nothing more happens.
Settled == AllDone /\ (\A i \in Invs : expired[i]) /\ UNCHANGED vars

Next ==
    \/ \E c \in Claims : ClaimFindTenant(c) \/ ClaimLoad(c) \/ ClaimSave(c) \/ ClaimFails(c)
    \/ \E i \in Revokes : RevokeLoad(i) \/ RevokeSave(i) \/ RevokeFails(i)
    \/ \E d \in DeleteReqs : DeleteLoad(d) \/ DeleteSave(d) \/ DeleteFails(d)
    \/ \E r \in Changes : ChangeFind(r) \/ ChangeLoad(r) \/ ChangeSave(r) \/ ChangeFails(r)
    \/ \E s \in Initiators : InitiateCheck(s) \/ InitiateSave(s) \/ InitiateFails(s)
    \/ \E i \in Invs : ClockPassesExpiry(i)
    \/ Settled

\* Each request keeps running while it can (no fairness on a failure), and time keeps passing.
Fairness ==
    /\ \A c \in Claims : WF_vars(ClaimFindTenant(c)) /\ WF_vars(ClaimLoad(c)) /\ WF_vars(ClaimSave(c))
    /\ \A i \in Revokes : WF_vars(RevokeLoad(i)) /\ WF_vars(RevokeSave(i))
    /\ \A d \in DeleteReqs : WF_vars(DeleteLoad(d)) /\ WF_vars(DeleteSave(d))
    /\ \A s \in Initiators : WF_vars(InitiateCheck(s)) /\ WF_vars(InitiateSave(s))
    /\ \A r \in Changes : WF_vars(ChangeFind(r)) /\ WF_vars(ChangeLoad(r)) /\ WF_vars(ChangeSave(r))
    /\ \A i \in Invs : WF_vars(ClockPassesExpiry(i))

Spec == Init /\ [][Next]_vars /\ Fairness

-----------------------------------------------------------------------------
\* Rules

UsersFrom(i) == {k \in UserIds : users[k].from = i}

\* Each sub has at most one live user (INV-6).
OneLiveUserPerSub == \A k, m \in UserIds : users[k].live /\ users[m].live /\ users[k].sub = users[m].sub => k = m

\* Every live user is in a live tenant (INV-7, INV-11).
LiveUserInLiveTenant == \A k \in UserIds : users[k].live => tenantLive[users[k].tenant]

\* Each invite leaves Pending at most once, so it has at most one terminal outcome (INV-5).
OneOutcomePerInvite == \A i \in Invs : moves[i] <= 1

\* Claimed means exactly one user came from the invite, and it is the user the invite names.
\* An invite that is not Claimed gave no user (INV-5, INV-7).
ClaimedMeansOneUser ==
    \A i \in Invs :
        IF inv[i].status = "Claimed"
        THEN UsersFrom(i) = {inv[i].by}
        ELSE UsersFrom(i) = {}

\* A claim that a revoke beat after the token lookup or the load gets no user.
\* Its answer is 410, or 404 when a tenant delete also took the invite, or 409 when the sub got a user meanwhile.
RevokeBeatsClaim == \A c \in Claims : revokedUnder[c] => cres[c] \in {"none", "410", "404", "409", "failed"}

\* Each live tenant has a live admin, so someone can still manage it (UM-4).
TenantKeepsAnAdmin ==
    \A t \in Tenants : tenantLive[t] => \E k \in UserIds : users[k].live /\ users[k].tenant = t /\ users[k].admin

\* Each run-again loop stops at MaxRuns.
RunsBounded ==
    /\ \A c \in Claims : cruns[c] <= MaxRuns
    /\ \A i \in Revokes : rruns[i] <= MaxRuns
    /\ \A d \in DeleteReqs : druns[d] <= MaxRuns
    /\ \A r \in Changes : mruns[r] <= MaxRuns

\* Liveness: every request ends with an answer (no request waits or loops forever).
EveryRequestEnds == <>AllDone

\* Liveness: a Pending invite always gets out: a claim, a revoke, the expiry, or the tenant delete.
PendingHasWayOut ==
    \A i \in Invs : (inv[i].status = "Pending" /\ ~inv[i].deleted /\ ~expired[i])
                    ~> (inv[i].status # "Pending" \/ inv[i].deleted \/ expired[i])
=============================================================================
