import { readdirSync, readFileSync } from 'node:fs'
import { join, sep } from 'node:path'

import { expect, it } from 'vitest'

// A page that a user opens from outside the app, so that no link in the SPA goes to it. /join: the link of an invite.
const ENTRY_PAGES = ['/join']

const srcFolder = import.meta.dirname

const sourceFiles = readdirSync(srcFolder, { recursive: true, encoding: 'utf8' })
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$|\.gen\.ts$/.test(file))
  .filter((file) => !file.split(sep).some((part) => ['examples', '(examples)', 'test'].includes(part)))
  .map((file) => ({ file, text: readFileSync(join(srcFolder, file), 'utf8') }))

const pageOf = (text: string) => text.match(/createFileRoute\(['"]([^'"]+)['"]\)/)?.[1].replace(/(?<=.)\/$/, '')

const linksIn = (text: string) =>
  [...text.matchAll(/\b(?:to|url|href)\s*(?:=\s*\{?|:\s*)['"`](\/[^'"`]*)['"`]/g)].map((match) => match[1])

// The wipe of the examples keeps a demo page that is outside (examples) and imports no example code. The only links to
// such a page are in the examples, so this test finds it. The without-examples job of CI also runs it after the wipe.
it('each page outside the examples has a link in the code that the wipe keeps', () => {
  const noLink = sourceFiles.flatMap(({ file, text }) => {
    const page = pageOf(text)
    if (!page || ENTRY_PAGES.includes(page)) return []
    const others = sourceFiles.filter((other) => other.file !== file)
    return others.some((other) => linksIn(other.text).includes(page)) ? [] : [`${page} (${file})`]
  })

  expect(
    noLink,
    'A page has no link in the core of the SPA. Put a demo page in routes/(examples)/. Give a page of the app a link. Add a page that opens from outside the app to ENTRY_PAGES.',
  ).toEqual([])
})
