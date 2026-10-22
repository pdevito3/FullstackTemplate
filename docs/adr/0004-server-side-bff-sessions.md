# Server-side BFF sessions in `bffdb`

> Superseded in part by [ADR 0007](0007-regular-repo-instead-of-template.md): `northflank.json` is deleted.

The BFF stores each BFF session on the server, in a new `bffdb` database on the same Postgres server as `appdb`. The cookie holds only an opaque key. A store seam has an EF Core adapter for production and an in-memory adapter for tests. The same `DbContext` holds the Data Protection keys and the replay cache of the logout tokens, and a Postgres advisory lock in `bffdb` stops two BFF instances from refreshing one refresh token at the same time. As a result, back-channel logout can end a BFF session with no browser present, and the BFF can run as many instances. The design is in [the home-rolled BFF spec](../specs/home-rolled-bff.md#31-bff-sessions-and-the-store).

## Considered options

- **Tokens in the cookie ticket**, as today. We did not choose this option. Back-Channel Logout 1.0 must end a BFF session from a `sid` or a `sub`, and a cookie cannot be found that way. The cookie also grows past one chunk.
- **A revocation list next to the cookie ticket.** We did not choose this option. It needs a server-side store anyway, and the cookie still holds the tokens.
- **A distributed cache (Redis or `HybridCache`).** We did not choose this option for now. It adds a resource to the stack, and a query by `sub` is not native to a cache. A cache adapter can come later behind the same seam.
- **A lock through a cache, or no lock.** We did not choose these options. With refresh token rotation, a lost race gives `invalid_grant`, and `invalid_grant` ends the BFF session. The advisory lock needs no new resource and releases itself if an instance stops.

## Consequences

- The BFF depends on Postgres. If `bffdb` is down, no user can sign in or use a BFF session.
- The BFF has its own `DbContext` and migrations. The AppHost and `northflank.json` give it `bffdb`.
- A deploy no longer logs out every user, because the Data Protection keys persist.
- Writes are conditional on the row and its `xmin`, and logout deletes the row first. Thus a late refresh cannot bring a BFF session back.
- A BFF session has a 30-day absolute limit, in addition to the 14-day sliding expiry. This is new behavior.
- A hosted service deletes the expired rows every 10 minutes in each instance.
