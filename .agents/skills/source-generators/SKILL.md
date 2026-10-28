---
name: source-generators
description: Picks a source generator before reflection. Use when you are about to call `GetProperties`, `GetCustomAttribute`, `Activator`, `MakeGenericType`, `new Regex`, or a `JsonSerializer` overload with no `JsonTypeInfo`, when you add a mapper, a regex, a value object, or a JSON payload, or when RS0030 names a reflection or regex API.
---

# Source generators

The template does most jobs of reflection with a source generator, at build time. Reflection is allowed only in four kinds of code. The generators, the kinds, and the sites of today are in `docs/backend/source-generators.md`. Read the doc before you start.

## Procedure

1. Name the job: a map, a match, a JSON payload, a value object, test data, or a type scan.
2. Find the job in the table "The generators of the template" of the doc. If a generator does the job, use it, and stop here.
   - **A map.** Write a Mapperly mapper. See `docs/backend/dtos-and-mappings.md`.
   - **A match.** Put `[GeneratedRegex]` on a static partial method.
   - **A JSON payload that the Server writes itself.** Add the type to `EntrypointJsonContext`. Then call the `JsonSerializer` overload that takes its `JsonTypeInfo`.
   - **A value object.** Make the class `partial` and derive it from `ValueObject`.
   - **Test data.** Use the `test-entity-graph` skill.
3. If the code is a startup scan, an OpenAPI transformer, a per-type cache, or a test, reflection is allowed. On a request path, cache the result for each type.
   - If the build gives RS0030, suppress it on the method. Start the justification with the kind, for example `Justification = "Startup scan. ..."`. Add the file to the list of `BannedApiSuppressionTests`.
   - If the API is not banned, put a comment above the call that names the kind.
4. If neither step fits, stop. Name the case in the pull request.
5. Build the Server, then run the unit tests. The build must have 0 warnings, and `BannedApiSuppressionTests` must pass.

## Review the result

Before you call the change done, make sure that each item is true:

- [ ] Each reflection call in the diff outside `tests/` has a kind comment or an RS0030 suppression. The justification names the kind.
- [ ] Each new RS0030 suppression is on the list of `BannedApiSuppressionTests`.
- [ ] Each reflection call on a request path reads each type one time, then keeps the result.
- [ ] No new reflection mapper or JSON call with no `JsonTypeInfo` does a job that a generator in the doc does.
