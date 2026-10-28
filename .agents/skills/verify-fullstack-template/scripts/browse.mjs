#!/usr/bin/env node
// Runs one recipe against the verification stack of this checkout, in a fresh Chromium context.
//
// Usage: node browse.mjs <recipe.mjs> [--headed] [--label <name>] [--sweep [--no-web-font]]
//
// A recipe is an ES module with a default export:
//   export default async ({ page, context, stack, expect, step, capture, settle, login, logout }) => { ... }
//
// Evidence goes to artifacts/verify/<RUN_ID>/<label>/ (label defaults to the recipe file name):
//   steps.log      each step with ok/FAIL and its duration
//   NN-<name>.png  a screenshot for each capture(), full-page by default
//   NN-<name>.aria.txt  the ARIA snapshot of the page for each capture()
//   NN-failure.png      the page when a step fails, before a finally block of the recipe runs
//   console.log    browser console errors and warnings, and page errors
//                  (an uncaught page error fails the run, even if all steps pass)
//   network.log    each response with status >= 400
//   trace.zip      the Playwright trace (open with: pnpm --dir frontend exec playwright show-trace <path>)
//   result.json    passed or failed, with the error and the page errors
//   sweep.md, sweep.json, sweep-NN.png   with --sweep, the layout and focus faults (see "Sweep" in SKILL.md)
import { execSync } from 'node:child_process'
import { appendFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import path from 'node:path'
import { pathToFileURL } from 'node:url'
import { blockWebFonts, report, routeLongValues, sweepCapture, sweepFocus, trackLastFocused } from './sweep.mjs'

const args = process.argv.slice(2)
const recipeArg = args.find((a) => !a.startsWith('--') && args[args.indexOf(a) - 1] !== '--label')
if (!recipeArg) {
  console.error('usage: node browse.mjs <recipe.mjs> [--headed] [--label <name>] [--sweep [--no-web-font]]')
  process.exit(2)
}
const headed = args.includes('--headed')
const labelIdx = args.indexOf('--label')
const label = labelIdx >= 0 ? args[labelIdx + 1] : path.basename(recipeArg, '.mjs')
const sweep = args.includes('--sweep')
const noWebFont = args.includes('--no-web-font')

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

// A step can fail while a promise of the recipe still waits, such as a page.waitForResponse that started before
// the failed click. That promise rejects when the browser closes. Without a handler, Node exits before result.json.
const unhandledRejections = []
process.on('unhandledRejection', (e) => {
  const message = e instanceof Error ? e.message : String(e)
  unhandledRejections.push(message)
  log('steps.log', `unhandled rejection: ${message.split('\n')[0]}`)
})

const browser = await chromium.launch({ headless: !headed })
const context = await browser.newContext({
  baseURL: stack.urls.web,
  ignoreHTTPSErrors: true,
  viewport: { width: 1280, height: 800 },
})
await context.tracing.start({ screenshots: true, snapshots: true })
if (sweep) {
  await routeLongValues(context, stack.urls.web)
  await context.addInitScript(trackLastFocused)
}
if (noWebFont) await blockWebFonts(context)
const page = await context.newPage()
const cdp = sweep ? await context.newCDPSession(page) : null
const sweepFaults = []
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

// The errors that already have a failure capture, so that an outer step or the runner does not capture again.
const capturedErrors = new Set()

async function step(name, fn) {
  const t0 = Date.now()
  try {
    const value = await fn()
    if (sweep) await sweepFocus({ page, settle, outDir, step: name, faults: sweepFaults })
    log('steps.log', `ok    ${name} (${Date.now() - t0} ms)`)
    console.log(`ok    ${name}`)
    return value
  } catch (e) {
    log('steps.log', `FAIL  ${name} (${Date.now() - t0} ms): ${e.message.split('\n')[0]}`)
    console.log(`FAIL  ${name}`)
    // Capture now. A finally block of the recipe, such as a logout(), runs after the error leaves this step.
    // A full-page capture scrolls the page, and a scroll closes an open overlay, so this capture shows the viewport.
    if (!capturedErrors.has(e)) await capture('failure', { fullPage: false }).then(() => capturedErrors.add(e), () => {})
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
  if (sweep && fullPage && name !== 'failure') await sweepCapture({ page, cdp, settle, outDir, capture: base, faults: sweepFaults })
}

// Logs in through the real BFF and the IdP of the stack, then returns the user's `name` claim from the BFF.
// `user` is a user of that IdP. Without it, the IdP's first user signs in.
//   ZeroFactor (default): admin@example.com (Admin User), user@example.com (Test User). One click signs in.
//   DuendeDemo: bob (Bob Smith), alice (Alice Smith). The password is the user name.
async function login(user) {
  await page.getByRole('button', { name: 'Login' }).click()
  switch (stack.authProvider.toLowerCase()) {
    case 'zerofactor':
      await signInAtZeroFactor(user ?? 'admin@example.com')
      break
    case 'duendedemo':
      await signInAtDuendeDemo(user ?? 'bob')
      break
    default:
      throw new Error(`login() cannot sign in at ${stack.authProvider}. See frontend/e2e/idps/ for its login form.`)
  }
  await page.waitForURL((u) => u.origin === new URL(stack.urls.web).origin, { waitUntil: 'domcontentloaded', timeout: 60_000 })
  await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible({ timeout: 15_000 })
  const claims = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).json())
  const name = claims.find((c) => c.type === 'name')?.value
  if (!name) throw new Error(`The BFF session of ${stack.authProvider} has no name claim.`)
  return { name }
}

// The 0factor login page lists the seeded users. A click on a user signs in.
async function signInAtZeroFactor(email) {
  await page.locator(`[data-testid="user-${email}"]`).click({ timeout: 30_000 })
}

// The demo IdP sometimes serves its CSS and scripts very slowly, so the page `load` event can take
// more than 30 s. Wait for the DOM and the form fields, never for `load`.
async function signInAtDuendeDemo(user) {
  await page.waitForURL(/demo\.duendesoftware\.com\/Account\/Login/, { waitUntil: 'domcontentloaded', timeout: 60_000 })
  const username = page.locator('#Input_Username')
  const password = page.locator('#Input_Password')
  await expect(username).toBeEditable({ timeout: 30_000 })
  await username.fill(user)
  await password.fill(user)
  await expect(username).toHaveValue(user)
  await expect(password).toHaveValue(user)
  await page.locator('button[value="login"]').click()
}

// Logs out through the BFF. 0factor sends the browser back at once. Duende shows a "return to the application"
// page, which this follows.
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
  if (!capturedErrors.has(e)) await capture('failure').catch(() => {})
} finally {
  await Promise.all(pendingConsoleErrors)
  // After a Ctrl-C or a browser crash, these throw. The run still writes result.json.
  await context.tracing.stop({ path: path.join(outDir, 'trace.zip') }).catch(() => {})
  await browser.close().catch(() => {})
  if (pageErrors.length > 0 && status === 'passed') {
    status = 'failed'
    error = `${pageErrors.length} page error(s) during the run`
  }
  if (unhandledRejections.length > 0 && status === 'passed') {
    status = 'failed'
    error = `unhandled rejection: ${unhandledRejections[0]}`
  }
  if (sweep) {
    const faults = sweepFaults.map(({ key, ...fault }) => fault)
    writeFileSync(path.join(outDir, 'sweep.json'), JSON.stringify(faults, null, 2))
    writeFileSync(path.join(outDir, 'sweep.md'), report(faults))
    if (faults.length > 0 && status === 'passed') {
      status = 'failed'
      error = `the sweep found ${faults.length} fault(s). See sweep.md`
    }
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
