# Commit the generated API contract

The frontend API contract comes from Kubb, and Kubb reads the OpenAPI document of the Server. We commit the OpenAPI document and the Kubb output, and we do not generate them only in CI or at dev-server start. As a result, a reviewer sees each change to the API surface in the pull request, and the frontend builds without a running Server or a .NET SDK.

## Consequences

- Kubb writes one file for each endpoint in each output kind (client, types, Zod). Two branches that change different endpoints change different files, so they seldom have a merge conflict.
- If a generated file has a conflict, regenerate it. Do not merge it by hand.
- The `API contract` CI workflow builds the Server, runs `pnpm generate:api`, and fails on a diff. Thus the committed files cannot drift from the Server code.
