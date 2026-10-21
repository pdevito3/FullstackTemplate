# Our own DPoP proof validation on the server

In phase 2, the server API validates DPoP proofs (RFC 9449) with our own code, on top of the Microsoft JWT bearer handler. The code checks the rules of RFC 9449 section 4.3: `typ`, an asymmetric `alg`, the signature with the header `jwk`, `htm`, `htu`, the `iat` window, `ath`, `cnf.jkt`, and the replay of `jti`. The replay cache is in `appdb`, so that all server instances share it. On the BFF side, `Duende.AccessTokenManagement` makes the proofs. The design is in [the home-rolled BFF spec](../specs/home-rolled-bff.md#9-phase-2-dpop).

## Considered options

- **`Duende.AspNetCore.Authentication.JwtBearer`.** It validates DPoP proofs on top of the Microsoft handler. We did not choose this option, because it has the commercial Duende license. That is the dependency that [ADR 0003](0003-replace-duende-bff.md) removes.
- **Wait for DPoP support in ASP.NET Core** (dotnet/aspnetcore#58016). We did not choose this option, because the issue has no release date.
- **A replay cache in memory.** We did not choose this option. With many server instances, a proof can replay on another instance.

## Consequences

- We own the code for the proof checks, and each rule of section 4.3 has its own test.
- If ASP.NET Core adds DPoP support, we can replace our code with it. The token format and the BFF do not change.
- The server writes one row for each proof to `appdb`, and a cleanup deletes the expired rows.
