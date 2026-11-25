import { RuleTester as EslintRuleTester, type Rule as EslintRule } from "eslint";
import type { Rule } from "@oxlint/plugins";
import tseslint from "typescript-eslint";
import { describe, it } from "vitest";

// Gives each rule its ESLint `create` method, so ESLint can run the rule objects that the tests import.
import "./index.ts";

EslintRuleTester.describe = describe;
EslintRuleTester.it = it;
EslintRuleTester.itOnly = it.only;

type Tests = Parameters<EslintRuleTester["run"]>[2];

/** The upstream tests run on the Oxlint RuleTester. This one runs them on ESLint with the parser of the repo. */
export class RuleTester {
  readonly #tester = new EslintRuleTester({ languageOptions: { parser: tseslint.parser } });

  /** Takes the Oxlint options of the upstream tests. The parser of the repo reads TypeScript, so they change nothing. */
  constructor(_oxlintConfig?: { languageOptions: { parserOptions: { lang: "ts" } } }) {}

  run(name: string, rule: Rule, tests: Tests): void {
    // SAFETY: eslintCompatPlugin in index.ts gave this rule object an ESLint `create` method. The Oxlint and ESLint
    // types of a rule do not overlap, because their contexts differ, so the cast goes through unknown.
    this.#tester.run(name, rule as unknown as EslintRule.RuleModule, tests);
  }
}
