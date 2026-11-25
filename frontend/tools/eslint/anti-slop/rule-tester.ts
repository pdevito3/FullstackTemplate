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

export class RuleTester {
  readonly #tester = new EslintRuleTester({ languageOptions: { parser: tseslint.parser } });

  constructor(_ignoredOxlintConfig?: { languageOptions: { parserOptions: { lang: "ts" } } }) {}

  run(name: string, rule: Rule, tests: Tests): void {
    // SAFETY: eslintCompatPlugin in index.ts gave this rule object an ESLint `create` method. The Oxlint and ESLint
    // types of a rule do not overlap, because their contexts differ, so the cast goes through unknown.
    this.#tester.run(name, rule as unknown as EslintRule.RuleModule, tests);
  }
}
