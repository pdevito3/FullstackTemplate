import { ESLint } from 'eslint'
import tseslint from 'typescript-eslint'
import { describe, expect, it } from 'vitest'
import { codeToWordsRule, noPageActionRule, stringSelectorMessage } from './eslint.config.js'

const pageAction = "import type { Page } from '@playwright/test'\nexport const save = (page: Page) => page.click('#save')\n"

async function messagesOf(ruleEntry) {
  const eslint = new ESLint({
    cwd: import.meta.dirname,
    overrideConfigFile: true,
    overrideConfig: {
      files: ['tests/**/*.ts'],
      languageOptions: {
        parser: tseslint.parser,
        // With CI=true, typescript-eslint reads the types from the files on disk, not from the text that this test lints
        parserOptions: {
          project: './tsconfig.tests.json',
          tsconfigRootDir: import.meta.dirname,
          disallowAutomaticSingleRunInference: true,
        },
      },
      plugins: { playwright: { rules: { 'no-page-action': noPageActionRule } } },
      rules: { 'playwright/no-page-action': ruleEntry },
    },
  })
  // The parser needs a file of the tests program. ESLint lints the text, not the file on disk.
  const [result] = await eslint.lintText(pageAction, { filePath: 'tests/support/fixtures.ts' })
  return result.messages.map((message) => message.message)
}

// The first lint builds the TypeScript program of the tests, which takes more than the 5 s default on a cold machine
describe('playwright/no-page-action', { timeout: 60_000 }, () => {
  it('reports a selector string with the message of its option', async () => {
    expect(await messagesOf(['error', 'Use a Locator.'])).toEqual(['Use a Locator.'])
  })

  it('reports a selector string with the locator message when the rule has no option', async () => {
    expect(await messagesOf('error')).toEqual([stringSelectorMessage])
  })
})

describe('words made from a code', () => {
  // The config of the app, so that a later block for src that replaces no-restricted-syntax fails the test
  const messagesFor = async (code) => {
    const eslint = new ESLint({ cwd: import.meta.dirname })
    const [result] = await eslint.lintText(code, { filePath: 'src/zone.ts' })
    return result.messages.map((message) => message.message)
  }

  it.each([
    "export const where = (zone) => zone.split('/').pop().replaceAll('_', ' ')",
    "export const where = (zone) => zone.replace(/_/g, ' ')",
    "export const where = (zone) => zone.replace('_', ' ')",
  ])('reports %s', async (code) => {
    expect(await messagesFor(code)).toEqual([codeToWordsRule.message])
  })

  it('accepts a replace that does not make words from a code', async () => {
    expect(await messagesFor("export const slug = (text) => text.replaceAll(' ', '_')")).toEqual([])
  })
})
