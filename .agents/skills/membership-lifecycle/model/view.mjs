// The /statechart view: the life of invite i1 as a tenant admin sees it in the invite list.
const invite = (v) => v.inv.i1;

export const title = "Invite i1: claim, revoke, expiry, and tenant delete";

export const state = (v) => {
  const i = invite(v);
  if (i.deleted) return i.status === "Claimed" ? "Deleted with the tenant (was Claimed)" : "Deleted with the tenant";
  if (i.status === "Pending") return v.expired.i1 ? "Expired" : "Pending";
  return i.status;
};

export const group = (v) => (invite(v).deleted ? "gone" : invite(v).status === "Pending" ? "open" : "final");
export const colors = { open: "blue", final: "teal", gone: "tan" };

const labels = {
  ClaimFindTenant: "the claim looks up the token",
  ClaimLoad: "the claim loads the invite",
  ClaimFails: "the claim fails, times out, or the server restarts",
  RevokeLoad: "the revoke loads the invite",
  RevokeFails: "the revoke fails, times out, or the server restarts",
  DeleteLoad: "the tenant delete takes the lock and loads the rows",
  DeleteFails: "the tenant delete fails, times out, or the server restarts",
  ChangeFind: "an admin's role change or user delete looks up the tenant",
  ChangeLoad: "the role change or user delete takes the lock, loads the admins, and decides",
  ChangeSave: "the role change or user delete commits",
  ChangeFails: "the role change or user delete fails, times out, or the server restarts",
  InitiateCheck: "a new user checks the sub",
  InitiateSave: "a new user starts a tenant",
  InitiateFails: "the tenant start fails",
  Settled: "every request has its answer",
  ClaimSave: "a claim commits and adds the user",
  RevokeSave: "an admin revokes the invite",
  ClockPassesExpiry: "the clock passes the expiry",
  DeleteSave: "an admin deletes the tenant",
};
export const step = (action) => labels[action] ?? action;
