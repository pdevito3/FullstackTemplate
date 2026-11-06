import { readdirSync, readFileSync } from 'node:fs'
import { join, sep } from 'node:path'

import ts from 'typescript'
import { expect, it } from 'vitest'

// A page with a way in that this test cannot read. /join: the link of an invite.
const PAGES_WITH_ANOTHER_WAY_IN = ['/join']

// The text of a string expression. `$` stands for each part that only the run knows, as it does in a route path.
function textOf(node: ts.Expression): string {
  if (ts.isStringLiteralLike(node)) return node.text
  if (ts.isTemplateExpression(node)) return node.head.text + node.templateSpans.map((span) => `$${span.literal.text}`).join('')
  if (ts.isBinaryExpression(node) && node.operatorToken.kind === ts.SyntaxKind.PlusToken) return textOf(node.left) + textOf(node.right)
  return '$'
}

// A syntax tree, so that a comment is not code. The ID in a route call names the route, so it is not a link.
function factsOf(file: string, text: string) {
  const strings: string[] = []
  let routeId: string | undefined
  const visit = (node: ts.Node) => {
    if (ts.isCallExpression(node) && ts.isIdentifier(node.expression) && /^create(Lazy)?FileRoute$/.test(node.expression.text)) {
      routeId = textOf(node.arguments[0])
      return
    }
    if (ts.isStringLiteralLike(node) || ts.isTemplateExpression(node) || ts.isBinaryExpression(node)) strings.push(textOf(node))
    ts.forEachChild(node, visit)
  }
  visit(ts.createSourceFile(file, text, ts.ScriptTarget.Latest, false, ts.ScriptKind.TSX))
  return { routeId, strings }
}

const srcFolder = import.meta.dirname

const sourceFiles = readdirSync(srcFolder, { recursive: true, encoding: 'utf8' })
  .filter((file) => /\.tsx?$/.test(file) && !/\.test\.tsx?$|\.gen\.ts$/.test(file))
  .filter((file) => !file.startsWith(`test${sep}`) && !file.split(sep).some((part) => ['examples', '(examples)'].includes(part)))
  .map((file) => ({ file, ...factsOf(file, readFileSync(join(srcFolder, file), 'utf8')) }))

// A route group and a pathless layout add no segment to the URL.
const urlOf = (routeId: string) =>
  `/${routeId
    .split('/')
    .filter((segment) => segment && !/^\(.*\)$|^_/.test(segment))
    .map((segment) => segment.replace(/_$/, ''))
    .join('/')}`

const goesTo = (url: string, text: string) => {
  const want = url.split('/')
  const got = text.split(/[?#]/)[0].replace(/(?<=.)\/$/, '').split('/')
  return want.length === got.length && want.every((segment, index) => (segment.includes('$') ? got[index] !== '' : segment === got[index]))
}

// The wipe of the examples keeps a demo page that is outside (examples) and imports no example code. The only links to
// such a page are in the examples, so this test finds it. The without-examples job of CI also runs it after the wipe.
//
// It reads strings, not links. So it passes when a string in core code has the URL and is not a link (`from: '/demo'`),
// and when a marked section has the link, until the wipe. It fails for a page with no whole URL in a string: a URL that
// starts with a variable (`${base}/things`), a relative link (`./edit`), `Route.to`, or a URL from the Server. Add such a
// page to the list above.
it('the URL of each page outside the examples is in the code that the wipe keeps', () => {
  const routeIds = sourceFiles.flatMap(({ routeId }) => routeId ?? [])
  const noLink = sourceFiles.flatMap(({ file, routeId }) => {
    // A layout route is no page of its own. Its child routes are.
    if (!routeId || routeIds.some((other) => other.startsWith(`${routeId}/`))) return []
    const url = urlOf(routeId)
    if (PAGES_WITH_ANOTHER_WAY_IN.includes(url)) return []
    const others = sourceFiles.filter((other) => other.file !== file)
    return others.some((other) => other.strings.some((text) => goesTo(url, text))) ? [] : [`${url} (${file})`]
  })

  expect(
    noLink,
    'No string in the core of the SPA has the URL of a page. Put a demo page in routes/(examples)/. Give a page of the app a link. Add a page that has another way in to PAGES_WITH_ANOTHER_WAY_IN.',
  ).toEqual([])
})
