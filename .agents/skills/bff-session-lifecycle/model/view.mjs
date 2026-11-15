// The words of the /statechart chart for BffSessionChart.cfg: one browser, one API request that refreshes,
// and one crash. Each function depends only on its arguments.

const refresh = (v) => {
  switch (v.pc[1]) {
    case "idle":
    case "done":
    case "fault":
      return null;
    case "refresh":
    case "waiter":
    case "lockwait":
      return "the refresh waits for the lock";
    case "locked":
    case "calling":
      return "lock held, the refresh calls the IdP";
    case "write":
      return "lock held, the refresh writes the new tokens";
    case "commit":
      return v.pend[1] === "write" ? "lock held, the refresh writes the new tokens" : "lock held, the refresh ends the session";
    case "rejectRevoke":
    case "permdelete":
      return "lock held, the refresh ends the session";
    default:
      return "lock held, the refresh ends with no change";
  }
};

const signedIn = (v) => v.row[1].exists && !v.row[1].expired;

export const state = (v) => {
  const r = refresh(v);
  if (signedIn(v)) return r ? `Signed in, ${r}` : "Signed in";
  if (v.logins > 0) return r ? `Signed out, ${r}` : "Signed out";
  if (["exchanged", "txn", "checked"].includes(v.lpc)) return "Tokens issued, no row yet";
  if (v.lpc === "authorized") return "At the IdP login";
  return "Not signed in";
};

export const group = (v) => (signedIn(v) ? "live" : "ended");
export const colors = { live: "teal", ended: "tan" };
export const title = "BFF session: login, the refresh lock, and a crash";

const labels = {
  IdpEndsSession: "the IdP session ends",
  LoginAuthorize: "you sign in at the IdP",
  LoginDeadlinePasses: "the deadline of the login passes",
  LoginExchange: "the code exchange gives tokens",
  LoginExchangeFails: "the code exchange fails",
  LoginEndsOldSession: "the new login deletes the old row",
  LoginBegins: "the login transaction starts",
  LoginChecksRecord: "the login reads the logout records",
  LoginCommits: "the login commits the row, or fails",
  ReqReads: "an API call reads the session",
  ReqJoinsInFlight: "the call starts the refresh",
  ReqWaiterEnds: "the waiting call gets the result",
  ReqTakesLock: "the call takes the refresh lock",
  ReqLockTimesOut: "the lock wait times out",
  ReqRereads: "the call reads the row again",
  ReqIdpRotates: "the IdP rotates the refresh token",
  ReqIdpRotatesResponseLost: "the IdP rotates, the answer is lost",
  ReqIdpRotatesIdTokenRejected: "the IdP rotates, the ID token fails the checks",
  ReqIdpRejects: "the IdP answers invalid_grant",
  ReqIdpUnreachable: "the IdP does not answer",
  ReqRevokesRejected: "the BFF revokes the rejected token",
  ReqDeletesRow: "the refresh deletes the row",
  ReqWritesTokens: "the refresh writes the tokens",
  ReqRevokesNew: "the BFF revokes the token that no row holds",
  ReqCommits: "the lock transaction commits",
  ReqEnds: "the lock transaction ends",
  ReqDbFailsAfterCall: "the database fails after the IdP call",
  ReqRenewsExpiry: "the call renews the expiry",
  LogoutReads: "you press logout",
  LogoutDeletes: "logout deletes the row",
  LogoutRevokes: "logout revokes the refresh token",
  LogoutSignsOut: "logout clears the cookie",
  BcDelivered: "the IdP sends the logout token",
  BcLocks: "the BFF takes the lock of the sid",
  BcInsertsJti: "the BFF writes the jti, or refuses a replay",
  BcInsertFails: "the jti write fails",
  BcFindsRows: "the BFF finds the rows of the sid",
  BcDeletesRow: "the BFF deletes a row of the sid",
  BcDeleteFails: "a row delete fails",
  BcAnswers200: "the BFF answers 200",
  RowExpires: "the session expires",
  CleanupDeletes: "the cleanup deletes the row",
  RecordExpires: "the cleanup deletes the logout record",
  Crash: "the BFF instance crashes",
  Finished: null,
};

export const step = (action) => (action in labels ? labels[action] : action);
