# anti-slop provenance

This folder is a vendored copy of the generic rules of [dmmulroy/anti-slop](https://github.com/dmmulroy/anti-slop). The repo owns these files. Change them to fit the rules of this repo.

- Source: https://github.com/dmmulroy/anti-slop
- Commit: `63dbb09f73438e550a6991dc6d778f979ffbf183`
- Copied: `src/` of that commit, as `frontend/tools/eslint/anti-slop/`, and its `LICENSE` (MIT)
- Dependency: `@oxlint/plugins` 1.78.0, the version that the upstream `package.json` of that commit pins

## Deviations

- ESLint 9 loads the plugin, not Oxlint. `eslintCompatPlugin` in `index.ts` gives each rule the ESLint `create` function. Node strips the types of the `.ts` files when `eslint.config.js` imports `index.ts`.
- The upstream tests run on ESLint. Each `rules/*.test.ts` imports `RuleTester` from `rule-tester.ts`, not from `oxlint/plugins-dev`. That is the only change in these files, except for one valid case in `no-shape-in-symbol-names.test.ts`. `pnpm test` runs them. `require-readable-spacing-cli.test.ts` is not copied, because it runs the Oxlint CLI. `eslint.config.test.js` proves that the lint of this repo reports each enabled rule.
- The rules read the offsets of a node from `range`, not from `start` and `end`. The typescript-eslint parser gives only `range`, so with `start` four rules gave a wrong result under ESLint. For example, `require-safety-comment-for-type-assertion` did not see a `SAFETY:` comment. The files are `shared/function-parameters.ts`, `no-widen-then-assert.ts`, `no-reduce-accumulator-copy.ts`, and `require-safety-comment-for-type-assertion.ts`.
- The Effect rules (`src/effect/`) are not copied. The frontend does not use Effect.
- ESLint has no `oxc/no-accumulating-spread`. The rule `anti-slop/no-accumulating-spread` in `rules/no-accumulating-spread.ts` is the local port of `crates/oxc_linter/src/rules/oxc/no_accumulating_spread.rs`, and its test runs the oxc cases. It resolves the spread argument by scope, as oxc does. It differs from oxc in these cases:
  - In a loop, oxc reports a spread only on the right side of the assignment to the variable, as in `all = [...all, n]`. This rule also reports the spread when the loop assigns it to the variable through a `const`, as in `const next = [...all, n]; all = next`. A spread of a variable that the loop assigns another value, such as `cur = cur.next`, is not reported. It also reports a parameter or a `var`, not only a `let`.
  - oxc reports the first parameter of any function with 2 to 4 parameters inside a `reduce` call. This rule reports it only when the function is the callback of the call.
  - A TypeScript `this` parameter does not count as the first parameter of the callback. oxc keeps it out of the parameters too.
  - oxc reads a method name in a template literal, as in ``arr[`reduce`]``. This rule does not.
- `no-shape-in-symbol-names` allows the right side of each qualified type name, such as `shape` in `typeof schema.shape` or `Shape` in `Types.Shape`. Upstream allows `schema.shape` in a value only.
