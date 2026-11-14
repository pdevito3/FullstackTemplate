---- MODULE MC ----
(* The bounds of a check. MC.cfg and Chart.cfg pick a request set and the break switches. *)
EXTENDS Membership

\* MC.cfg: two subs race for invite i1, s1 also claims i2, an admin revokes i1,
\* s2 starts its own tenant, an admin deletes T1 and can delete users.
FullClaims == {"c1", "c2", "c3"}
FullSub(c) == CASE c = "c1" -> "s1" [] c = "c2" -> "s2" [] c = "c3" -> "s1"
FullInv(c) == CASE c = "c1" -> "i1" [] c = "c2" -> "i1" [] c = "c3" -> "i2"
FullInvs == {"i1", "i2"}
FullRevokes == {"i1"}
FullInitiators == {"s2"}

\* Chart.cfg: one claim, one revoke, one tenant delete, on one invite. Small enough to draw.
ChartClaims == {"c1"}
ChartSub(c) == "s1"
ChartInv(c) == "i1"
ChartInvs == {"i1"}
ChartRevokes == {"i1"}
NoInitiators == {}
====
