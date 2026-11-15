---- MODULE MC ----
(* The bounds of a check. MC.cfg and Chart.cfg pick a request set and the break switches. *)
EXTENDS Membership

\* MC.cfg: two subs race for invite i1, s1 also claims i2, an admin revokes i1,
\* s2 starts its own tenant, an admin deletes T1. T1 starts with the admins a1 and a2. One admin demotes a1
\* while another removes a2, and an admin removes s1.
FullClaims == {"c1", "c2", "c3"}
FullSub(c) == CASE c = "c1" -> "s1" [] c = "c2" -> "s2" [] c = "c3" -> "s1"
FullInv(c) == CASE c = "c1" -> "i1" [] c = "c2" -> "i1" [] c = "c3" -> "i2"
FullInvs == {"i1", "i2"}
FullRevokes == {"i1"}
FullInitiators == {"s2"}
FullAdmins == <<"a1", "a2">>
FullChanges == {"m1", "u1", "u2"}
FullChangeSub(r) == CASE r = "m1" -> "a1" [] r = "u1" -> "a2" [] r = "u2" -> "s1"
FullChangeKind(r) == IF r = "m1" THEN "demote" ELSE "delete"

\* Live.cfg: the race of the two admins only.
RaceChanges == {"m1", "u1"}

\* Chart.cfg: one claim, one revoke, one tenant delete, on one invite. Small enough to draw.
ChartClaims == {"c1"}
ChartSub(c) == "s1"
ChartInv(c) == "i1"
ChartInvs == {"i1"}
ChartRevokes == {"i1"}
NoInitiators == {}
OneAdmin == <<"a1">>
NoChanges == {}
====
