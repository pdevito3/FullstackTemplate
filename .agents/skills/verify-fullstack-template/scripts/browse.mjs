#!/usr/bin/env node
// Runs one recipe against the verification stack of this checkout, in a fresh Chromium context.
//
// Usage: node browse.mjs <recipe.mjs> [--headed] [--label <name>]
//
// A recipe is an ES module with a default export:
//   export default async ({ page, context, stack, expect, step, capture, settle, login, logout }) => { ... }
//
// Evidence goes to artifacts/verify/<RUN_ID>/<label>/ (label defaults to the recipe file name):
//   steps.log      each step with ok/FAIL and its duration
//   NN-<name>.png  a screenshot for each capture(), full-page by default
//   NN-<name>.aria.txt  the ARIA snapshot of the page for each capture()
//   console.log    browser console errors and warnings, and page errors
//                  (an uncaught page error fails the run, even if all steps pass)
//   network.log    each response with status >= 400
//   trace.zip      the Playwright trace (open with: pnpm --dir frontend exec playwright show-trace <path>)
//   result.json    passed or failed, with the error and the page errors
import { execSync } from 'node:child_process'
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const args = process.argv.slice(2)
const recipeArg = args.find((a) => !a.startsWith('--') && args[args.indexOf(a) - 1] !== '--label')
if (!recipeArg) {
  console.error('usage: node browse.mjs <recipe.mjs> [--headed] [--label <name>]')
  process.exit(2)
}
const headed = args.includes('--headed')
const labelIdx = args.indexOf('--label')
const label = labelIdx >= 0 ? args[labelIdx + 1] : path.basename(recipeArg, '.mjs')

const root = execSync('git rev-parse --show-toplevel').toString().trim()
const currentFile = path.join(root, 'artifacts/verify/.current')
if (!existsSync(currentFile)) {
  console.error('browse: no current run. Run launch.sh first.')
  process.exit(2)
}
const runDir = path.join(root, 'artifacts/verify', readFileSync(currentFile, 'utf8').trim())
const stack = JSON.parse(readFileSync(path.join(runDir, 'stack.json'), 'utf8'))
// A second run of the same label gets its own directory, so that the evidence of each attempt stays apart.
let outDir = path.join(runDir, label)
for (let n = 2; existsSync(outDir); n++) outDir = path.join(runDir, `${label}-${n}`)
mkdirSync(outDir, { recursive: true })

// Playwright is a dev dependency of the frontend. Resolve it from there.
const requireFromFrontend = createRequire(path.join(root, 'frontend/package.json'))
const { chromium, expect } = requireFromFrontend('@playwright/test')

const log = (file, line) => appendFileSync(path.join(outDir, file), line + '\n')
let captureCount = 0

const browser = await chromium.launch({ headless: !headed })
const context = await browser.newContext({
  baseURL: stack.urls.web,
  ignoreHTTPSErrors: true,
  viewport: { width: 1280, height: 800 },
})
await context.tracing.start({ screenshots: true, snapshots: true })
const page = await context.newPage()
// An uncaught error in the page is a bug, even if the UI looks right. Collect each one to fail the run at the end.
// A console error is a bug too: an error boundary catches a render crash and only logs it. The browser also
// logs each failed response as a console error. network.log has those, so they do not fail the run.
const pageErrors = []
const pendingConsoleErrors = []
page.on('console', (m) => {
  if (m.type() === 'error' || m.type() === 'warning') log('console.log', `[${m.type()}] ${m.text()}`)
  if (m.type() !== 'error' || m.text().startsWith('Failed to load resource')) return
  // React logs a crash as console.error('%o ... %s', error, ...). Read the error from the arguments, not from the format string.
  pendingConsoleErrors.push(
    Promise.all(m.args().map((a) => a.evaluate((v) => (v instanceof Error ? v.message : String(v)))))
      .then((parts) => parts.filter((part) => part.replace(/%[osdifcO]/g, '').trim() !== '').join(' ').replace(/\s+/g, ' ') || m.text())
      .catch(() => m.text())
      .then((text) => pageErrors.push(`console.error: ${text}`)),
  )
})
page.on('pageerror', (e) => {
  pageErrors.push(e.message)
  log('console.log', `[pageerror] ${e.message}`)
})
page.on('response', (r) => {
  if (r.status() >= 400) log('network.log', `${r.status()} ${r.request().method()} ${r.url()}`)
})

async function step(name, fn) {
  const t0 = Date.now()
  try {
    const value = await fn()
    log('steps.log', `ok    ${name} (${Date.now() - t0} ms)`)
    console.log(`ok    ${name}`)
    return value
  } catch (e) {
    log('steps.log', `FAIL  ${name} (${Date.now() - t0} ms): ${e.message.split('\n')[0]}`)
    console.log(`FAIL  ${name}`)
    throw e
  }
}

// Motion and CSS animations run on enter and exit. Wait for them before a capture or a click in an overlay.
// Playwright retries a click on a moving element with a forced scroll, and React Aria closes a popover on scroll.
// The two frames let a scroll event arrive before the next action.
function settle() {
  return page.evaluate(async () => {
    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)))
    // Skip infinite animations, such as the Skeleton pulse. Their finished promise never resolves.
    // Skip paused animations too, and cap the wait, so that a stuck animation cannot hang the run.
    const finite = document
      .getAnimations()
      .filter((animation) => animation.playState === 'running' && animation.effect?.getComputedTiming().endTime !== Infinity)
    await Promise.race([
      Promise.all(finite.map((animation) => animation.finished.catch(() => {}))),
      new Promise((resolve) => setTimeout(resolve, 2000)),
    ])
  })
}

// A full-page screenshot resizes the viewport to the page height. A React Aria Sheet (the Drawer)
// closes on that resize, so capture an open overlay with { fullPage: false }.
async function capture(name, { fullPage = true } = {}) {
  await settle()
  const base = `${String(++captureCount).padStart(2, '0')}-${name}`
  await page.screenshot({ path: path.join(outDir, `${base}.png`), fullPage })
  const aria = await page.locator('body').ariaSnapshot()
  writeFileSync(path.join(outDir, `${base}.aria.txt`), `# ${page.url()}\n${aria}\n`)
  log('steps.log', `cap   ${base} ${page.url()}`)
}

// Logs in through the real BFF and Duende demo flow. Users: bob/bob, alice/alice.
// The demo IdP sometimes serves its CSS and scripts very slowly, so the page `load` event can take
// more than 30 s. Wait for the DOM and the form fields, never for `load`.
async function login(user = 'bob') {
  await page.getByRole('button', { name: 'Login' }).click()
  await page.waitForURL(/demo\.duendesoftware\.com\/Account\/Login/, { waitUntil: 'domcontentloaded', timeout: 60_000 })
  const username = page.locator('#Input_Username')
  const password = page.locator('#Input_Password')
  await expect(username).toBeEditable({ timeout: 30_000 })
  await username.fill(user)
  await password.fill(user)
  await expect(username).toHaveValue(user)
  await expect(password).toHaveValue(user)
  await page.locator('button[value="login"]').click()
  await page.waitForURL((u) => u.origin === new URL(stack.urls.web).origin, { waitUntil: 'domcontentloaded', timeout: 60_000 })
  await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible({ timeout: 15_000 })
}

// Logs out through the BFF. Duende shows a "return to the application" page, which this follows.
async function logout() {
  await page.getByRole('button', { name: 'Logout' }).click()
  const back = page.locator('a[href*="signout-callback-oidc"]')
  await Promise.race([
    back.waitFor({ timeout: 20_000 }).then(() => back.click()),
    page.getByRole('button', { name: 'Login' }).waitFor({ timeout: 20_000 }),
  ])
  await expect(page.getByRole('button', { name: 'Login' })).toBeVisible({ timeout: 20_000 })
}

let status = 'passed'
let error = null
try {
  const recipe = await import(pathToFileURL(path.resolve(recipeArg)).href)
  await recipe.default({ page, context, stack, expect, step, capture, settle, login, logout })
} catch (e) {
  status = 'failed'
  error = e.message
  await capture('failure').catch(() => {})
} finally {
  await Promise.all(pendingConsoleErrors)
  await context.tracing.stop({ path: path.join(outDir, 'trace.zip') })
  await browser.close()
  if (pageErrors.length > 0 && status === 'passed') {
    status = 'failed'
    error = `${pageErrors.length} page error(s) during the run`
  }
  writeFileSync(
    path.join(outDir, 'result.json'),
    JSON.stringify(
      { recipe: recipeArg, label, status, error, pageErrors, web: stack.urls.web, finishedAt: new Date().toISOString() },
      null,
      2,
    ),
  )
}

console.log(`${status === 'passed' ? 'PASSED' : 'FAILED'}  ${label}  evidence: ${outDir}`)
if (error) console.log(error)
for (const message of pageErrors) console.log(`[pageerror] ${message}`)
process.exit(status === 'passed' ? 0 : 1)
