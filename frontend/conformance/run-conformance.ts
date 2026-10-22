/**
 * Runs the OpenID conformance suite RP test plans of section 7 of docs/specs/home-rolled-bff.md against the BFF.
 *
 * Start the stack first, from the checkout root:
 *   AuthProvider=Conformance dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
 * Then, in frontend/: pnpm test:conformance
 *
 * The script starts the suite with docker compose (docker-compose-prebuilt.yml and docker-compose.override.yml), if
 * it does not run yet. The suite answers the discovery document of the alias only while a test module runs, and the
 * BFF reads it at startup. So the AppHost does not start the BFF with this provider. The script starts the BFF again
 * for each test module, after the module waits for the RP. A Playwright browser then goes through /bff/login, and
 * through /bff/logout for the logout plans. The suite pages need no user input.
 *
 * Each FAILED result fails the run. Each WARNING or REVIEW result fails the run, unless accepted-results.json lists
 * it with a reason. Each module that does not finish is "not run" and fails the run: it has no result, so a green run
 * would hide a plan that tests nothing. The results, the HTML export of each plan, and the screenshots go to
 * conformance/results/.
 *
 * Arguments: the names of the plans to run (default: all). CONFORMANCE_SUITE_URL changes the suite URL.
 */
import { execFileSync, spawnSync } from 'node:child_process'
import { appendFileSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium, request, type APIRequestContext, type Browser } from '@playwright/test'

const here = dirname(fileURLToPath(import.meta.url))
const apphost = join(here, '..', '..', 'FullstackTemplate.AppHost')
const resultsDir = join(here, 'results')
// The AppHost gives this path to the BFF as Auth:PinnedCertificatePath
const certificatePath = join(here, '.suite', 'suite-certificate.pem')
const suiteUrl = (process.env.CONFORMANCE_SUITE_URL ?? 'https://localhost.emobix.co.uk:8443').replace(/\/$/, '')

// The same values as ConformanceAuthProvider in the AppHost
const alias = 'fullstacktemplate-bff'
const clientId = 'fullstacktemplate-bff'
const clientSecret = 'conformance-secret-for-local-test-plans-only'

// The longest wait for a module to finish after the browser steps. A negative test waits 5 s for the RP to stop.
const finishTimeoutMs = 30_000

type Flow = 'login' | 'login-twice' | 'login-logout' | 'login-refresh'

interface PlanDefinition {
  name: string
  // The variants that the plan does not fix
  variant: Record<string, string>
  flow: Flow
}

const staticClient = { client_registration: 'static_client', request_type: 'plain_http_request' }
const codeFlow = { ...staticClient, client_auth_type: 'client_secret_basic', response_mode: 'default' }

const finishedResults = ['PASSED', 'SKIPPED', 'WARNING', 'REVIEW', 'FAILED']

const plans: PlanDefinition[] = [
  { name: 'oidcc-client-basic-certification-test-plan', variant: staticClient, flow: 'login' },
  { name: 'oidcc-client-config-certification-test-plan', variant: codeFlow, flow: 'login' },
  { name: 'oidcc-client-rp-initiated-logout-rp-basic', variant: codeFlow, flow: 'login-logout' },
  { name: 'oidcc-client-back-channel-logout-rp-basic', variant: codeFlow, flow: 'login-logout' },
  // Not part of the certification program. The happy path module finishes on a userinfo call with the refreshed
  // access token. The BFF does not make it, but the server does, because it checks the opaque access tokens of the
  // suite at the userinfo endpoint (Auth:AccessTokenValidation in ConformanceAuthProvider). The negative modules
  // send an ID token with a wrong iss or sub in the refresh response, and the BFF must not use the new tokens.
  { name: 'oidcc-client-refreshtoken-test-plan', variant: { ...codeFlow, response_type: 'code' }, flow: 'login-refresh' },
]

// The key rotation module of the config plan needs a second login, with the new keys
const flowOverrides: Record<string, Flow> = { 'oidcc-client-test-signing-key-rotation': 'login-twice' }

interface AcceptedResult {
  plan: string
  module: string
  result: string
  reason: string
}

interface ModuleResult {
  plan: string
  module: string
  id?: string
  status: string
  result: string
  accepted?: string
  blocking: boolean
  steps: string[]
}

interface DescribedResource {
  displayName: string
  urls?: { name: string; url: string }[]
}

function aspire(...args: string[]) {
  return spawnSync('dotnet', ['aspire', ...args, '--apphost', apphost, '--non-interactive'], { encoding: 'utf8' })
}

function bffUrls(): { https: string } | undefined {
  const described = JSON.parse(
    execFileSync('dotnet', ['aspire', 'describe', '--format', 'Json', '--apphost', apphost], {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'inherit'],
    }),
  ) as { resources: DescribedResource[] }
  const https = described.resources.find((r) => r.displayName === 'bff')?.urls?.find((u) => u.name === 'https')?.url
  return https ? { https } : undefined
}

/** Starts the BFF, or starts it again, and waits until it is healthy. False when it does not get healthy. */
function restartBff(): boolean {
  const restart = aspire('resource', 'bff', 'restart')
  if (restart.status !== 0) {
    aspire('resource', 'bff', 'start')
  }
  return aspire('wait', 'bff', '--status', 'healthy', '--timeout', '60').status === 0
}

function compose(...args: string[]) {
  const files = ['-f', join(here, 'docker-compose-prebuilt.yml'), '-f', join(here, 'docker-compose.override.yml')]
  return execFileSync('docker', ['compose', '-p', 'fullstacktemplate-conformance', ...files, ...args], {
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'inherit'],
  })
}

async function sleep(ms: number) {
  await new Promise((resolve) => setTimeout(resolve, ms))
}

async function startSuite(api: APIRequestContext) {
  const ready = async () => (await api.get('api/runner/available').catch(() => undefined))?.ok() ?? false
  if (!(await ready())) {
    console.log('Starting the conformance suite')
    compose('up', '-d')
  }
  for (let attempt = 0; !(await ready()); attempt++) {
    if (attempt > 120) throw new Error(`The conformance suite at ${suiteUrl} does not answer.`)
    await sleep(2000)
  }
  // The self-signed certificate of the suite, that the BFF pins
  mkdirSync(dirname(certificatePath), { recursive: true })
  writeFileSync(certificatePath, compose('exec', '-T', 'nginx', 'cat', '/etc/ssl/certs/nginx-selfsigned.crt'))
}

async function createPlan(api: APIRequestContext, plan: PlanDefinition, config: object) {
  const response = await api.post('api/plan', {
    params: { planName: plan.name, variant: JSON.stringify(plan.variant) },
    data: config,
  })
  if (response.status() !== 201) throw new Error(`Could not create the plan ${plan.name}: ${await response.text()}`)
  return (await response.json()) as { id: string; modules: { testModule: string; variant: Record<string, string> }[] }
}

async function createModule(api: APIRequestContext, planId: string, module: string, variant: Record<string, string>) {
  const response = await api.post('api/runner', { params: { test: module, plan: planId, variant: JSON.stringify(variant) } })
  if (response.status() !== 201) throw new Error(`Could not create the module ${module}: ${await response.text()}`)
  return ((await response.json()) as { id: string }).id
}

async function moduleInfo(api: APIRequestContext, id: string) {
  return (await (await api.get(`api/info/${id}`)).json()) as { status: string; result: string | null }
}

async function waitForStatus(api: APIRequestContext, id: string, statuses: string[], timeoutMs: number) {
  const end = Date.now() + timeoutMs
  for (;;) {
    const info = await moduleInfo(api, id)
    if (statuses.includes(info.status) || Date.now() > end) return info
    await sleep(500)
  }
}

/**
 * Starts the BFF while a throwaway module answers the discovery document, so that the URL of the BFF is known
 * before the plans are created. The port stays the same when the BFF starts again.
 */
async function warmUp(api: APIRequestContext) {
  const config = { alias, description: 'warm-up', client: { client_id: clientId, client_secret: clientSecret, redirect_uri: 'https://localhost/' } }
  const plan = await createPlan(api, plans[0], config)
  const id = await createModule(api, plan.id, plan.modules[0].testModule, plan.modules[0].variant)
  await waitForStatus(api, id, ['WAITING'], 30_000)
  const healthy = restartBff()
  const urls = bffUrls()
  await api.delete(`api/runner/${id}`)
  if (!healthy || !urls) throw new Error('The BFF does not start against the conformance suite. Read its logs: dotnet aspire logs bff')
  return urls.https.replace(/\/$/, '')
}

async function drive(browser: Browser, bff: string, flow: Flow, screenshot: string, steps: string[]) {
  const context = await browser.newContext({ ignoreHTTPSErrors: true })
  const page = await context.newPage()
  // The SPA calls the API on its own after the login, on the origin of the Vite dev server. Each API call can make a
  // refresh, and the server checks the token at the userinfo endpoint of the suite. A request after the module
  // finished fails it. So only the driver calls the API, with context.request, which this route does not stop.
  await page.route(
    (url) => url.pathname.startsWith('/api/') && url.origin !== new URL(suiteUrl).origin,
    (route) => route.abort(),
  )
  const step = async (name: string, action: () => Promise<string>) => {
    try {
      steps.push(`${name}: ${await action()}`)
    } catch (error) {
      steps.push(`${name}: ${(error as Error).message.split('\n')[0]}`)
    }
  }
  const login = async () => {
    const callback = page.waitForResponse((r) => r.url().startsWith(`${bff}/signin-oidc`), { timeout: 20_000 })
    await page.goto(`${bff}/bff/login`, { waitUntil: 'commit' }).catch(() => undefined)
    return `callback ${(await callback).status()}`
  }
  const user = async () => {
    const response = await context.request.get(`${bff}/bff/user`, { headers: { 'X-CSRF': '1' } })
    return { status: response.status(), claims: response.ok() ? ((await response.json()) as { type: string; value: string }[]) : [] }
  }

  await step('login', login)
  await step('user', async () => `${(await user()).status}`)
  if (flow === 'login-twice') {
    await step('second login', login)
  }
  if (flow === 'login-refresh') {
    // Token management refreshes the access token before it expires, on a call to the API
    await step('api', async () => `${(await context.request.get(`${bff}/api/v1/weather/secure`, { headers: { 'X-CSRF': '1' } })).status()}`)
  }
  if (flow === 'login-logout') {
    await step('logout', async () => {
      const logoutUrl = (await user()).claims.find((c) => c.type === 'bff:logout_url')?.value
      if (!logoutUrl) return 'no bff:logout_url claim'
      const signOutCallback = page.waitForResponse((r) => r.url().startsWith(`${bff}/signout-callback-oidc`), { timeout: 20_000 })
      await page.goto(`${bff}${logoutUrl}`, { waitUntil: 'commit' })
      return `signout callback ${(await signOutCallback).status()}`
    })
    await step('user after logout', async () => `${(await user()).status}`)
  }
  await page.screenshot({ path: screenshot, fullPage: true }).catch(() => undefined)
  await context.close()
}

async function runPlan(api: APIRequestContext, browser: Browser, bff: string, plan: PlanDefinition, accepted: AcceptedResult[]) {
  const config = {
    alias,
    description: `FullstackTemplate BFF: ${plan.name}`,
    waitTimeoutSeconds: 5,
    client: {
      client_id: clientId,
      client_secret: clientSecret,
      redirect_uri: `${bff}/signin-oidc`,
      post_logout_redirect_uri: `${bff}/signout-callback-oidc`,
      // The suite server container calls the BFF on the host
      backchannel_logout_uri: `${bff.replace('//localhost', '//host.docker.internal')}/bff/backchannel`,
    },
  }
  const created = await createPlan(api, plan, config)
  const planDir = join(resultsDir, plan.name)
  mkdirSync(planDir, { recursive: true })
  console.log(`\n${plan.name} (${created.id}): ${created.modules.length} modules`)

  const results: ModuleResult[] = []
  for (const { testModule, variant } of created.modules) {
    const result: ModuleResult = { plan: plan.name, module: testModule, status: 'NOT_RUN', result: 'NOT_RUN', blocking: false, steps: [] }
    try {
      result.id = await createModule(api, created.id, testModule, variant)
      await waitForStatus(api, result.id, ['WAITING', 'FINISHED', 'INTERRUPTED'], 30_000)
      // A new BFF reads the discovery document and the signing keys of this module. It does not get healthy when
      // the module gives a bad discovery document, which is the expected result of that module.
      const healthy = restartBff()
      result.steps.push(`bff ${healthy ? 'healthy' : 'not healthy'}`)
      // A discovery module can finish when the BFF reads the discovery document at startup. A request after the
      // finish fails the module.
      const started = await moduleInfo(api, result.id)
      if (healthy && started.status === 'WAITING') {
        await drive(browser, bff, flowOverrides[testModule] ?? plan.flow, join(planDir, `${testModule}.png`), result.steps)
      } else {
        result.steps.push(`no browser steps (module ${started.status})`)
      }
      const info = await waitForStatus(api, result.id, ['FINISHED', 'INTERRUPTED'], finishTimeoutMs)
      result.status = info.status
      // A failure in the suite interrupts the module with the result FAILED. That module ran.
      const ran = info.status === 'FINISHED' || (info.status === 'INTERRUPTED' && info.result === 'FAILED')
      result.result = ran ? (info.result ?? 'UNKNOWN') : `NOT_FINISHED (${info.status})`
      if (info.status !== 'FINISHED') await api.delete(`api/runner/${result.id}`)
    } catch (error) {
      result.steps.push(`error: ${(error as Error).message.split('\n')[0]}`)
    }
    if (!finishedResults.includes(result.result)) {
      // Not run: no result, so it fails the run in each plan
      result.blocking = true
    } else if (!['PASSED', 'SKIPPED'].includes(result.result)) {
      // Only a WARNING or a REVIEW can be accepted. Each FAILED fails the run.
      const entry = ['WARNING', 'REVIEW'].includes(result.result)
        ? accepted.find((a) => a.plan === plan.name && a.module === testModule && a.result === result.result)
        : undefined
      result.accepted = entry?.reason
      result.blocking = !entry
    }
    const mark = result.blocking ? 'FAIL' : result.accepted ? 'accepted' : 'ok'
    console.log(`  ${result.result.padEnd(9)} ${testModule} [${mark}] ${result.steps.join('; ')}`)
    results.push(result)
  }

  const exported = await api.get(`api/plan/exporthtml/${created.id}`, { timeout: 120_000 })
  if (exported.ok()) {
    writeFileSync(join(planDir, `${plan.name}-${created.id}.zip`), await exported.body())
  } else {
    console.log(`  The HTML export of the plan failed with ${exported.status()}`)
  }
  return results
}

function summarize(results: ModuleResult[]) {
  const lines = [
    '## OpenID conformance suite',
    '',
    '| Plan | Status | Passed | Skipped | Warning | Review | Failed | Not run | Blocking |',
    '|---|---|---|---|---|---|---|---|---|',
  ]
  for (const plan of plans.filter((p) => results.some((r) => r.plan === p.name))) {
    const planResults = results.filter((r) => r.plan === plan.name)
    const count = (predicate: (r: ModuleResult) => boolean) => planResults.filter(predicate).length
    const notRun = count((r) => !finishedResults.includes(r.result))
    const status = notRun === 0 ? 'ran' : notRun === planResults.length ? 'NOT RUN' : 'INCOMPLETE'
    lines.push(
      `| ${plan.name} | ${status} | ${count((r) => r.result === 'PASSED')} | ${count((r) => r.result === 'SKIPPED')} | ${count((r) => r.result === 'WARNING')} | ${count((r) => r.result === 'REVIEW')} | ${count((r) => r.result === 'FAILED')} | ${notRun} | ${count((r) => r.blocking)} |`,
    )
  }
  const notes = results.filter((r) => r.blocking || r.accepted)
  if (notes.length > 0) {
    lines.push('', '| Plan | Module | Result | Note |', '|---|---|---|---|')
    for (const r of notes) {
      const note = !finishedResults.includes(r.result)
        ? `Blocking: not run. ${r.steps.join('; ').replaceAll('|', '\\|')}`
        : r.blocking
          ? 'Blocking'
          : `Accepted: ${r.accepted}`
      lines.push(`| ${r.plan} | ${r.module} | ${r.result} | ${note} |`)
    }
  }
  return lines.join('\n') + '\n'
}

async function main() {
  const selected = process.argv.slice(2)
  const unknown = selected.filter((name) => !plans.some((p) => p.name === name))
  if (unknown.length > 0) throw new Error(`Unknown plans: ${unknown.join(', ')}. Use ${plans.map((p) => p.name).join(', ')}.`)
  const accepted = JSON.parse(readFileSync(join(here, 'accepted-results.json'), 'utf8')) as AcceptedResult[]

  if (existsSync(resultsDir)) rmSync(resultsDir, { recursive: true })
  mkdirSync(resultsDir, { recursive: true })

  const api = await request.newContext({ baseURL: `${suiteUrl}/`, ignoreHTTPSErrors: true })
  const browser = await chromium.launch()
  const results: ModuleResult[] = []
  try {
    await startSuite(api)
    const bff = await warmUp(api)
    console.log(`BFF ${bff}, suite ${suiteUrl}`)
    for (const plan of plans.filter((p) => selected.length === 0 || selected.includes(p.name))) {
      results.push(...(await runPlan(api, browser, bff, plan, accepted)))
    }
  } finally {
    await browser.close()
    await api.dispose()
    // Keep the logs of the BFF for the report
    writeFileSync(join(resultsDir, 'bff.log'), aspire('logs', 'bff', '-n', '5000').stdout ?? '')
  }

  const summary = summarize(results)
  writeFileSync(join(resultsDir, 'results.json'), JSON.stringify(results, null, 2))
  writeFileSync(join(resultsDir, 'summary.md'), summary)
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary)
  console.log(`\n${summary}\nThe results are in ${resultsDir}`)
  process.exit(results.some((r) => r.blocking) ? 1 : 0)
}

await main()
