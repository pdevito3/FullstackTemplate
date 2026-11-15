// The words of the chart for /statechart. One attachment, a1, seen as a user and an operator see it.
const a = "a1";

const queued = (v) => ["due", "leased"].includes(v.req[a].st) || ["due", "leased"].includes(v.req.tenant.st);
const deleting = (v) => v.req[a].st === "leased" || v.req.tenant.st === "leased";

export const title = "An attachment and its object";

export const state = (v) => {
  const apc = v.apc[a];
  const tenant = v.tenant === "deleted" ? "tenant deleted" : null;
  const tag = (s) => (tenant && !s.startsWith("Tenant") ? `${s}, ${tenant}` : s);
  if (apc === "put") return tag("Upload started");
  if (["lock", "check", "commit"].includes(apc)) return tag(v.obj[a] ? "Object stored, row saving" : "Upload started");
  if (["cleanCheck", "cleanDelete"].includes(apc)) return tag("Save failed, cleanup runs");
  if (v.row[a] === "live") return "File in the list";
  if (v.obj[a] && queued(v)) return deleting(v) ? "Object delete running" : "Object delete queued";
  if (v.obj[a] && (v.req[a].st === "dead" || v.req.tenant.st === "dead")) return "Object delete dead, a person must run it";
  if (v.obj[a]) return v.old[a] ? "Object left, old enough for the sweep" : "Object left, younger than the grace";
  if (v.row[a] === "deleted") return "File deleted";
  if (apc === "done") return tag("Add failed, no object");
  return tag("No file");
};

export const group = (v) => (v.tenant === "deleted" ? "tenantDeleted" : "tenantLive");
export const colors = { tenantLive: "blue", tenantDeleted: "orange" };

const labels = {
  StartAdd: "a user uploads a file",
  Put: "the put returns",
  TakeShared: "the save takes the tenant lock",
  CheckOwner: "the save checks the owner of the attachment",
  CommitRow: "the save commits",
  ResolveCommit: "the commit ends",
  StopAdd: "the user cancels the upload",
  CleanupCheck: "the cleanup checks for the row",
  CleanupDelete: "the cleanup deletes the object",
  CrashAdder: "the process dies",
  DeleteAttachment: "a user deletes the file",
  TakeExclusive: "a tenant delete takes the lock",
  CommitTenantDelete: "the tenant is deleted",
  CrashTenantDelete: "the process dies",
  Claim: "a worker claims the delete job",
  Work: "the job calls storage",
  DeletePage: "the job deletes the listed objects",
  Complete: "the job succeeds",
  Fail: "the attempt fails",
  LeaseExpires: "the lease runs out",
  CrashRunner: "the process dies",
  Age: "the grace passes",
  SweepList: "the sweep lists the bucket",
  SweepCheck: "the sweep reads the live rows",
  SweepDelete: "the sweep deletes the objects with no row",
  SweepFail: "a delete of the sweep fails",
  Done: null,
};

// A put or a save that throws goes to the cleanup, so it gets its own words
export const step = (action, from, to) => {
  if (action === "Put" && to.apc[a] !== "lock") return "the put fails";
  if (action === "CommitRow" && to.apc[a] === "cleanCheck") return "the save throws";
  if (action === "CleanupCheck" && to.apc[a] === "done" && to.obj[a] && !["live", "deleted"].includes(to.row[a]))
    return "the cleanup check fails";
  return action in labels ? labels[action] : action;
};
