---
name: api-contract
description: Regenerates and checks the Kubb API contract of the SPA. Use when you change an endpoint, a DTO, an endpoint name or tag, or an OpenAPI transformer, when the `api-contract` CI job fails, when a generated file has a merge conflict, or when a generated type in `frontend/src/api/generated*/` is wrong.
---

# API contract

Kubb makes the SPA client from the OpenAPI document that a Debug build of the Server writes. The contract is the document in `FullstackTemplate.Server/openapi/` and the Kubb output in `frontend/src/api/generated*/`. Both are committed. See `docs/adr/0001-commit-generated-api-contract.md`. The layout of the output is in `docs/frontend.md#api-contract`.

Regenerate after a change to any of these:

- An endpoint route, its name (`.WithName`), its tag (`.WithTags`), or its statuses
- A request DTO or a response DTO
- An attribute that a transformer reads, such as `[NamesOf<T>]`, `[SchemaMaxLength]`, `[SchemaMinimum]`, or `[SchemaMaximum]`
- A transformer in `FullstackTemplate.Server/Resources/Entrypoints/Http/OpenApi/`
- `SupportedVersions`. To add or remove a version, also follow `docs/backend.md#api-versioning`

## Procedure

1. From the repository root, build the Server: `dotnet build FullstackTemplate.Server --no-incremental`. The build must end with 0 errors. It writes one document for each API version: `FullstackTemplate.Server.json` for v1, and `FullstackTemplate.Server_v<n>.json` for each later version.
   - Use the Debug configuration, the default. A build in another configuration writes nothing.
   - A build with nothing to compile does not write the document again. `--no-incremental` makes it write the document.
   - A failed build keeps the old document. Then step 2 gives the old contract and shows no error.
2. In `frontend/`, run `pnpm generate:api`.
3. Stage the contract: `git add FullstackTemplate.Server/openapi frontend/src/api`. Run `pnpm generate:api` again. Then `git diff --exit-code -- FullstackTemplate.Server/openapi 'frontend/src/api/generated*'` must show nothing. A diff here means that a generated file changed after step 2, or that two runs give different output.
4. In `frontend/`, run `pnpm build`, `pnpm lint`, `pnpm test`, and `pnpm test:e2e`. The `api-contract` job runs the same four. `tsc` compiles the generated files, so a renamed or removed field fails at each caller.
5. Commit the Server change, the document, and the generated files in one commit.

## Fix the cause on the Server

Do not edit a file in `frontend/src/api/generated*/` or `FullstackTemplate.Server/openapi/`. These files are output. Kubb writes them again on each run, so change only the Server and regenerate.

- **A generated type is wrong.** Find the property in the document. Find the transformer or the attribute that sets it in `docs/frontend.md#a-generated-type-is-wrong`. Fix the DTO, the attribute, or the transformer. A transformer can read attributes with reflection. Use the `source-generators` skill for the RS0030 rule. Add a test in `OpenApiTransformerTests` (unit) or `OpenApiDocumentTests` (functional). Then do the procedure.
- **A generated file has a merge conflict.** Take either side of each generated file and of the document. Then do the procedure.
- **The `api-contract` job fails.** Do the procedure on your branch and commit the result. The job deletes the committed contract, builds the Server, runs `pnpm generate:api`, and fails on a diff.

## Use the generated contract in the SPA

The rules are in `docs/frontend/server-state.md` and `docs/frontend.md`.

- Call a generated client only in a hand-written hook in `domain/[area]/apis/`, with its key factory. Do not write an API function or a DTO type by hand.
- Give the `signal` of the query function to the client.
- A list client (an operation name that ends in `List`) returns the full response. The hook reads the `X-Pagination` header with `parsePagination`.
- Pick `throwOnError` by the error mode that `docs/frontend.md#api-client` gives.
- A form takes each limit from the generated Zod schema of its DTO, with `stringWithMaxLengthOf`, `maxLengthOf`, `minimumOf`, or `maximumOf` from `lib/api-contract.ts`. If a function throws because the contract has no limit, add `[SchemaMaxLength]`, `[SchemaMinimum]`, or `[SchemaMaximum]` to the request DTO on the Server and regenerate. See `docs/frontend.md#forms`.

## Review the result

Before you call the change done, make sure that each item is true:

- [ ] Step 3 shows no diff.
- [ ] Step 4 passes.
- [ ] The diff has no hand change under `frontend/src/api/generated*/` or `FullstackTemplate.Server/openapi/`.
- [ ] The Server change, the document, and the generated files are in one commit.
