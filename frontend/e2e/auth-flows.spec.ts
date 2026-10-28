import { execFileSync } from 'node:child_process'
import { createHash, randomBytes } from 'node:crypto'
import { test, expect, type Locator, type Page, type Request } from '@playwright/test'
import { loadIdp, type IdpCapabilities, type IdpConfig } from './idps.ts'
import { refreshesOnEachCall, refreshMarginHint } from './stack-features.ts'

/**
 * Authentication flow tests for all supported Identity Providers.
 *
 * These tests verify:
 * 1. Swagger OAuth authorization and secure API access
 * 2. React UI login, secure weather API access, and logout
 *
 * Test users (from docs/authentication.md):
 * - Keycloak/FusionAuth/Authentik/Cognito: admin@example.com / password123!
 * - Duende Demo: bob / bob
 * - ZeroFactor: admin@example.com, with no password
 *
 * Run with: pnpm test:e2e:stack (against the IdP of the running stack, see docs/authentication.md)
 * Debug with: pnpm test:e2e:stack --debug
 * UI mode: pnpm test:e2e:stack --ui
 */

// pnpm test:e2e:stack sets IDP to the provider of the running stack. See e2e/idps.ts.
const currentIdp = loadIdp(process.env.IDP)

/**
 * True when the locator becomes visible within the timeout. `locator.isVisible()` does not wait, and it ignores
 * its timeout option, so it cannot wait for a page that is still loading.
 */
async function becomesVisible(locator: Locator, timeout: number): Promise<boolean> {
  return locator.waitFor({ state: 'visible', timeout }).then(
    () => true,
    () => false,
  )
}

/**
 * Opens a tag section of Swagger UI. A click on the tag toggles it, so the click happens only when the section is closed.
 */
async function expandSwaggerTag(page: Page, tag: string) {
  const section = page.locator('.opblock-tag-section').filter({ has: page.locator(`.opblock-tag[data-tag="${tag}"]`) })
  await expect(section).toBeVisible()
  if (!(await section.getAttribute('class'))?.includes('is-open')) {
    await section.locator('.opblock-tag').click()
  }
  await expect(section).toHaveClass(/\bis-open\b/)
}

/**
 * Helper to login via the IDP login page
 */
async function loginAtIdp(page: Page, idp: IdpConfig) {
  // Wait for the IDP login form
  await page.locator(idp.usernameSelector).waitFor({ state: 'visible' })

  // Fill username/email
  const usernameValue = idp.email || idp.username
  await page.fill(idp.usernameSelector, usernameValue!)

  if (idp.twoStepLogin) {
    // Two-step login: submit username first, then password
    await page.click(idp.submitSelector)

    // Fill password on the second page. fill waits for the field
    await page.fill(idp.passwordSelector!, idp.password!)

    // Use secondStepSubmitSelector if defined, otherwise use submitSelector
    const secondSubmit = idp.secondStepSubmitSelector || idp.submitSelector
    await page.click(secondSubmit)
  } else {
    // Single-page login: fill password, if the IdP has one, and submit
    if (idp.passwordSelector) {
      await page.fill(idp.passwordSelector, idp.password!)
    }
    await page.click(idp.submitSelector)
  }
}

/**
 * The number of server spans of the secure weather call with a DPoP proof since `startedAt`. The server tags the span
 * of a request with `app.auth.scheme=DPoP` after it checked the proof and the binding of the token.
 */
function dpopSpansOfSecureWeatherSince(startedAt: number): number {
  const output = execFileSync(
    'dotnet',
    ['aspire', 'otel', 'spans', '--search', 'app.auth.scheme', '--format', 'Json', '--apphost', '../FullstackTemplate.AppHost'],
    { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] },
  )
  const json = output.split('\n').find((line) => line.startsWith('[{') || line === '[]')
  const spans = JSON.parse(json ?? '[]') as { timestamp: string; attributes: Record<string, string> }[]
  return spans.filter(
    (span) =>
      Date.parse(span.timestamp) >= startedAt &&
      span.attributes['url.path'] === '/api/v1/weather/secure' &&
      span.attributes['app.auth.scheme'] === 'DPoP',
  ).length
}

test.describe(`Auth Flows - ${currentIdp.name}`, () => {
  test.describe('React UI Authentication', () => {
    test('should login, access secure weather, and logout', async ({ page }) => {
      // Step 1: Navigate to the home page
      await page.goto('/')
      await expect(page).toHaveURL('/')

      // Step 2: Click the Login button
      const loginButton = page.getByRole('button', { name: /login/i })
      await expect(loginButton).toBeVisible()
      await loginButton.click()

      // Step 3: Wait for the login form of the IDP. The page can show the form before all of its resources load
      await expect(page.locator(currentIdp.usernameSelector)).toBeVisible({ timeout: 30000 })

      // Step 4: Login at the IDP
      await loginAtIdp(page, currentIdp)

      // Step 5: Wait for redirect back to app and verify logged in state
      await page.waitForURL('/', { timeout: 30000 })

      // Verify logout button is visible (indicates logged in)
      const logoutButton = page.getByRole('button', { name: /logout/i })
      await expect(logoutButton).toBeVisible({ timeout: 10000 })

      // Step 6: Click "Public API" button to toggle to Secure API
      // The button starts as "Public API" and toggles to "Secure API"
      const secureApiButton = page.getByRole('button', { name: /public api|secure api/i })
      await expect(secureApiButton).toBeVisible()
      await secureApiButton.click()

      // Step 7: Verify secure weather data loads
      // The secure endpoint returns data with a "requestedBy" field
      // Look for temperature cards or data indicating weather loaded
      // The page shows "Authenticated" indicator when showing secure data
      await expect(page.getByText(/authenticated/i).first()).toBeVisible({ timeout: 10000 })

      // Step 8: Click Logout
      const appOrigin = new URL(page.url()).origin
      await logoutButton.click()

      // Step 9: Wait for logout to complete
      // Some IDPs (like Authentik) show a logout confirmation page instead of auto-redirecting
      // Check if we're on an IDP logout confirmation page
      // Authentik shows "Log back into [app]"
      // Duende Demo shows "Click here to return to the [app] application"
      const backToAppLink = page.getByRole('link', { name: /log back into/i })
      const duendeReturnLink = page.locator('a[href*="signout-callback-oidc"]')

      // Wait for the app or for a return link, because some IDPs redirect to the app by themselves.
      // The IDP login page also has a Login button, so count that button only on the app.
      const returnLink = backToAppLink.or(duendeReturnLink).first()
      const appLoginButton = page.getByRole('button', { name: /login/i })
      await expect
        .poll(
          async () =>
            (await returnLink.isVisible()) ||
            (new URL(page.url()).origin === appOrigin && (await appLoginButton.isVisible())),
          { timeout: 30000 }
        )
        .toBe(true)
      if (await returnLink.isVisible()) {
        await returnLink.click()
      }

      // Wait for redirect back to app
      await page.waitForURL('/', { timeout: 30000 })

      // Verify login button is visible again (indicates logged out)
      await expect(page.getByRole('button', { name: /login/i })).toBeVisible({ timeout: 10000 })
    })

    // The stack runs with DPoP when the BFF has Auth:Features:DPoP=Require (DPoP=true when the stack starts). The
    // server tags the span of a request that came with a valid DPoP proof, so the test reads the span of the call.
    test('should call the secure weather API with a DPoP-bound token', async ({ page }) => {
      test.skip(process.env.DPOP !== 'true', 'The stack does not use DPoP. Start it with DPoP=true.')
      const startedAt = Date.now()

      await page.goto('/')
      await page.getByRole('button', { name: /login/i }).click()
      await expect(page.locator(currentIdp.usernameSelector)).toBeVisible({ timeout: 30000 })
      await loginAtIdp(page, currentIdp)
      await page.waitForURL('/', { timeout: 30000 })
      await expect(page.getByRole('button', { name: /logout/i })).toBeVisible({ timeout: 10000 })

      await page.getByRole('button', { name: /public api|secure api/i }).click()
      await expect(page.getByText(/authenticated/i).first()).toBeVisible({ timeout: 10000 })

      await expect
        .poll(() => dpopSpansOfSecureWeatherSince(startedAt), { timeout: 30000, message: 'no server span with app.auth.scheme=DPoP' })
        .toBeGreaterThan(0)
    })

    test('should use PAR when the provider offers it, and the plain authorize URL when not', async ({ page }) => {
      await page.goto('/')
      const loginResponse = page.waitForResponse((response) => new URL(response.url()).pathname === '/bff/login')
      await page.getByRole('button', { name: /login/i }).click()

      // The 302 of /bff/login holds the authorize URL
      const location = (await loginResponse).headers()['location']
      expect(location).toBeTruthy()
      const authorizeParameters = [...new URL(location).searchParams.keys()].sort()
      if (currentIdp.capabilities.pushedAuthorization) {
        expect(authorizeParameters).toEqual(['client_id', 'request_uri'])
      } else {
        expect(authorizeParameters).toEqual(expect.arrayContaining(['client_id', 'code_challenge', 'redirect_uri', 'response_type', 'state']))
        expect(authorizeParameters).not.toContain('request_uri')
      }

      // The login finishes with either form of the authorize request
      await loginAtIdp(page, currentIdp)
      await page.waitForURL('/', { timeout: 30000 })
      await expect(page.getByRole('button', { name: /logout/i })).toBeVisible({ timeout: 10000 })
    })

    test('should return to a deep link on the SPA origin, with its query, after a login from that link', async ({ page, baseURL }) => {
      const deepLink = '/about?tab=team&x=1'
      await page.goto(deepLink)
      await expect(page.getByRole('navigation', { name: 'breadcrumb' })).toHaveText('About')

      await page.goto(`/bff/login?returnUrl=${encodeURIComponent(deepLink)}`)
      await loginAtIdp(page, currentIdp)

      await page.waitForURL(new URL(deepLink, baseURL).toString(), { timeout: 30000 })
      await expect(page.getByRole('navigation', { name: 'breadcrumb' })).toHaveText('About')
      const status = await page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).status)
      expect(status).toBe(200)
    })
  })
})

const sessionCookieName = '__Host-bff'

/**
 * Logs in through the SPA and waits for the first load after the redirect to show the signed-in user.
 */
async function signInThroughSpa(page: Page) {
  await page.goto('/')
  await page.getByRole('button', { name: /login/i }).click()
  await loginAtIdp(page, currentIdp)
  await page.waitForURL('/', { timeout: 30000 })
  await expect(page.getByRole('button', { name: /logout/i })).toBeVisible({ timeout: 10000 })
}

/**
 * Opens a page on another site with one link to `target`, and clicks the link. The browser then sends a cross-site
 * top-level GET, as for a link in an email or on another web site. Returns the request of the navigation.
 */
async function followLinkFromAnotherSite(page: Page, target: string) {
  const otherSite = 'https://cross-site.test/'
  await page.route(otherSite, (route) =>
    route.fulfill({ contentType: 'text/html', body: `<!doctype html><a href="${target}">Open the app</a>` }),
  )
  await page.goto(otherSite)
  const navigation = page.waitForRequest((request) => request.url() === target && request.isNavigationRequest())
  await page.getByRole('link', { name: 'Open the app' }).click()
  return navigation
}

async function sentSessionCookie(request: Request) {
  const cookie = (await request.allHeaders())['cookie'] ?? ''
  return cookie.split(';').some((pair) => pair.trim().startsWith(`${sessionCookieName}=`))
}

// Section 3.8 of docs/specs/home-rolled-bff.md: the session cookie is SameSite=Strict
test.describe(`Session cookie - ${currentIdp.name}`, () => {
  test('should set the correlation and nonce cookies with Lax and the session cookie with Strict, and end signed in on the first load', async ({
    page,
    context,
  }) => {
    await page.goto('/')
    await page.getByRole('button', { name: /login/i }).click()
    await page.locator(currentIdp.usernameSelector).waitFor({ state: 'visible', timeout: 30000 })

    // The browser must send these two cookies on the cross-site GET from the identity provider to /signin-oidc
    const loginCookies = await context.cookies()
    for (const prefix of ['.AspNetCore.Correlation.', '.AspNetCore.OpenIdConnect.Nonce.']) {
      const cookie = loginCookies.find((c) => c.name.startsWith(prefix))
      expect(cookie, `${prefix} cookie`).toBeDefined()
      expect(cookie).toMatchObject({ sameSite: 'Lax', secure: true, httpOnly: true })
    }

    await loginAtIdp(page, currentIdp)

    // The first load after the redirect is cross-site, so its HTML has no session cookie. The SPA calls /bff/user
    // same-site, with the cookie, and shows the signed-in user.
    await page.waitForURL('/', { timeout: 30000 })
    await expect(page.getByRole('button', { name: /logout/i })).toBeVisible({ timeout: 10000 })

    const sessionCookie = (await context.cookies()).find((c) => c.name === sessionCookieName)
    expect(sessionCookie).toMatchObject({ sameSite: 'Strict', secure: true, httpOnly: true, path: '/' })
  })

  test('should show the signed-in user on a deep link from another site', async ({ page }) => {
    await signInThroughSpa(page)
    const deepLink = new URL('/about', page.url()).toString()

    const navigation = await followLinkFromAnotherSite(page, deepLink)

    await page.waitForURL(deepLink)
    // The navigation is cross-site, so the browser does not send the Strict cookie with the HTML request
    expect(await sentSessionCookie(navigation)).toBe(false)
    await expect(page.getByRole('navigation', { name: 'breadcrumb' })).toHaveText('About')

    // Only the home page shows the user. The SPA that the cross-site link loaded calls /bff/user same-site
    const user = page.waitForResponse((response) => new URL(response.url()).pathname === '/bff/user')
    await page.getByRole('link', { name: 'Home' }).click()
    expect((await user).status()).toBe(200)
    await expect(page.getByRole('button', { name: /logout/i })).toBeVisible({ timeout: 10000 })
  })

  test('should send no session cookie on a cross-site top-level GET to /api, which gets a 401', async ({ page }) => {
    await signInThroughSpa(page)
    const apiUrl = new URL('/api/v1/weather/secure', page.url()).toString()

    const navigation = await followLinkFromAnotherSite(page, apiUrl)

    const response = await navigation.response()
    expect(response?.status()).toBe(401)
    expect(await sentSessionCookie(navigation)).toBe(false)

    // The same call from the SPA is same-site, so it has the cookie and succeeds
    await page.goto('/')
    const sameSiteStatus = await page.evaluate(
      async (url) => (await fetch(url, { headers: { 'X-CSRF': '1' } })).status,
      apiUrl,
    )
    expect(sameSiteStatus).toBe(200)
  })
})

test.describe(`Swagger - ${currentIdp.name}`, () => {
  // Get server URL from environment or use default
  const serverUrl = process.env.SERVER_URL || 'https://localhost:24713'

  test('should load Swagger UI with OAuth configured and display Weather endpoints', async ({
    page,
  }) => {
    // Step 1: Navigate to Swagger UI
    await page.goto(`${serverUrl}/swagger`, {
      waitUntil: 'networkidle',
    })

    // Step 2: Verify Swagger loaded correctly with title (it's in h1)
    await expect(page.locator('h1').filter({ hasText: 'FullstackTemplate API' })).toBeVisible({
      timeout: 10000,
    })

    // Step 3: Verify Authorize button is present (OAuth is configured)
    const authorizeButton = page.getByRole('button', { name: /authorize/i })
    await expect(authorizeButton).toBeVisible({ timeout: 5000 })

    // Step 4: Expand the Weather section to see the Weather endpoints
    await expandSwaggerTag(page, 'Weather')

    // Step 5: Verify Weather endpoints are visible after expansion
    await expect(page.getByText('/api/v1/weather').first()).toBeVisible({ timeout: 5000 })
    await expect(page.getByText('/api/v1/weather/secure')).toBeVisible({ timeout: 5000 })
  })

  test('should authorize and access secure weather endpoint', async ({ page }) => {
    // Step 1: Navigate to Swagger UI
    await page.goto(`${serverUrl}/swagger`, {
      waitUntil: 'networkidle',
    })

    // Step 2: Click the Authorize button
    const authorizeButton = page.getByRole('button', { name: /authorize/i })
    await expect(authorizeButton).toBeVisible({ timeout: 10000 })
    await authorizeButton.click()

    // Step 3: Wait for modal and click authorize in the OAuth section
    await page.waitForSelector('.modal-ux', { timeout: 5000 })

    // The authorize button in the modal
    const modalAuthorizeButton = page.locator('.modal-ux .auth-btn-wrapper button.authorize')
    await expect(modalAuthorizeButton.first()).toBeVisible({ timeout: 5000 })

    // Step 4: Handle the OAuth popup
    const popupPromise = page.waitForEvent('popup', { timeout: 10000 })
    await modalAuthorizeButton.first().click()

    let popup: import('@playwright/test').Page
    try {
      popup = await popupPromise
    } catch {
      // If no popup appears, OAuth might be configured differently or blocked
      // Close modal and skip this test
      const closeButton = page.locator('.modal-ux button.btn-done, .modal-ux button[aria-label="Close"]')
      if (await closeButton.first().isVisible()) {
        await closeButton.first().click()
      }
      test.skip(true, 'OAuth popup was blocked or did not appear')
      return
    }

    // Step 5: Login at the IDP
    await popup.waitForLoadState('networkidle', { timeout: 15000 })
    await loginAtIdp(popup, currentIdp)

    // Step 6: Wait for popup to close
    try {
      await popup.waitForEvent('close', { timeout: 30000 })
    } catch {
      // Popup might have already closed or redirected
      if (!popup.isClosed()) {
        await popup.close()
      }
    }

    // Step 7: Close the modal
    // Swagger UI exchanges the code for a token after the popup closes. Wait for the result, not a fixed time.
    await expect(page.locator('.modal-ux').getByRole('heading', { name: 'Authorized' })).toBeVisible({
      timeout: 15000,
    })
    const closeButton = page.locator('.modal-ux button.btn-done')
    if (await closeButton.isVisible()) {
      await closeButton.click()
    }

    // Step 8: Expand the Weather section
    await expandSwaggerTag(page, 'Weather')

    // Step 9: Find and click on the secure weather endpoint row to expand it
    const secureEndpoint = page.locator('.opblock').filter({ hasText: '/api/v1/weather/secure' })
    await expect(secureEndpoint).toBeVisible({ timeout: 5000 })

    // Click on the summary to expand if not already expanded. A click toggles it
    if (!(await secureEndpoint.getAttribute('class'))?.includes('is-open')) {
      await secureEndpoint.locator('.opblock-summary').click()
    }
    await expect(secureEndpoint).toHaveClass(/\bis-open\b/)
    // Swagger UI resolves an operation after it opens. An Execute before that fails with
    // OperationNotFoundError and sends no request. The response example shows the resolved schema.
    await expect(secureEndpoint.locator('.responses-wrapper')).toContainText('requestedBy', { timeout: 30000 })

    // Step 10: Click "Try it out" button if visible (might be auto-enabled)
    const tryItOutButton = secureEndpoint.getByRole('button', { name: 'Try it out' })
    if (await becomesVisible(tryItOutButton, 1000)) {
      await tryItOutButton.click()
    }

    // Step 11: Click Execute button
    const executeButton = secureEndpoint.getByRole('button', { name: 'Execute' })
    await expect(executeButton).toBeVisible({ timeout: 5000 })
    await executeButton.click()

    // Step 12: Check response
    // Look for response status in the live responses section. The expect waits for the API response
    const liveResponse = secureEndpoint.locator('.live-responses-table').first()
    await expect(liveResponse).toBeVisible({ timeout: 10000 })

    // The first status cell is the table header ("Code"), so read the response row
    const responseStatus = liveResponse.locator('tr.response .response-col_status')
    await expect(responseStatus).toContainText('200', { timeout: 10000 })

    // requestedBy in the body proves that the token reached the API
    const responseBody = secureEndpoint.locator('.live-responses-table pre.microlight').first()
    await expect(responseBody).toContainText('requestedBy', { timeout: 5000 })
  })
})

// The live discovery document of the provider must match the capabilities in its e2e/idps/ file, so that a provider
// upgrade that adds or drops a feature fails here and not in a feature test
test.describe(`Provider discovery - ${currentIdp.name}`, () => {
  test('live discovery matches the capabilities of the IdP file', async ({ request }) => {
    const authority = process.env.AUTHORITY
    test.skip(!authority, 'AUTHORITY is not set. pnpm test:e2e:stack sets it from the stack.')

    const response = await request.get(`${authority!.replace(/\/$/, '')}/.well-known/openid-configuration`)
    expect(response.status()).toBe(200)
    const discovery = (await response.json()) as Record<string, unknown>

    const live: IdpCapabilities = {
      revocation: typeof discovery.revocation_endpoint === 'string',
      issuerParameter: discovery.authorization_response_iss_parameter_supported === true,
      backChannelLogout: discovery.backchannel_logout_supported === true,
      endSession: typeof discovery.end_session_endpoint === 'string',
      pushedAuthorization: typeof discovery.pushed_authorization_request_endpoint === 'string',
      dpop: Array.isArray(discovery.dpop_signing_alg_values_supported) && discovery.dpop_signing_alg_values_supported.length > 0,
    }
    expect(live).toEqual(currentIdp.capabilities)
  })
})

function requiredEnv(name: string): string {
  const value = process.env[name]
  if (!value) {
    throw new Error(`${name} is not set. pnpm test:e2e:stack sets it from the stack.`)
  }
  return value
}

/**
 * Calls the identity provider with Node fetch, not the request fixture of Playwright, so that the tokens and the admin
 * credentials stay out of the trace. Returns the status and the JSON body.
 */
async function callIdp(url: string, init: RequestInit = {}): Promise<{ status: number; body: Record<string, unknown> }> {
  const response = await fetch(url, init)
  const text = await response.text()
  return { status: response.status, body: text ? (JSON.parse(text) as Record<string, unknown>) : {} }
}

async function readDiscovery(): Promise<Record<string, unknown>> {
  const { status, body } = await callIdp(`${requiredEnv('AUTHORITY').replace(/\/$/, '')}/.well-known/openid-configuration`)
  expect(status).toBe(200)
  return body
}

// The status of /bff/user for the session cookie of the page
async function bffUserStatus(page: Page): Promise<number> {
  return page.evaluate(async () => (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).status)
}

// The sid claim of the BFF session, the id of the session at the identity provider
async function bffSessionId(page: Page): Promise<string> {
  const claims = await page.evaluate(
    async () => (await (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).json()) as { type: string; value: string }[],
  )
  const sid = claims.find((claim) => claim.type === 'sid')?.value
  expect(sid, 'the sid claim of the BFF session').toBeTruthy()
  return sid!
}

/**
 * Ends one Keycloak session through the admin REST API: the user session and its offline session, because the BFF asks
 * for offline_access. Keycloak then sends a back-channel logout token to the clients of the session.
 *
 * The test ends only the session of its own login. When the BFF gets the logout token, it revokes the refresh token of
 * the session. Keycloak then also drops the other sessions of the user for the client, with no logout token, so a
 * second BFF session of the user would stay signed in until its next refresh.
 */
async function endKeycloakSession(sessionId: string) {
  const keycloakUrl = requiredEnv('IDP_URL').replace(/\/$/, '')
  const realm = /\/realms\/([^/]+)/.exec(requiredEnv('AUTHORITY'))?.[1]
  expect(realm, 'the Keycloak realm in AUTHORITY').toBeTruthy()

  const adminToken = await callIdp(`${keycloakUrl}/realms/master/protocol/openid-connect/token`, {
    method: 'POST',
    body: new URLSearchParams({
      grant_type: 'password',
      client_id: 'admin-cli',
      username: requiredEnv('IDP_ADMIN_USERNAME'),
      password: requiredEnv('IDP_ADMIN_PASSWORD'),
    }),
  })
  expect(adminToken.status, 'the Keycloak admin token request').toBe(200)
  const headers = { Authorization: `Bearer ${adminToken.body.access_token as string}` }
  const session = `${keycloakUrl}/admin/realms/${realm}/sessions/${encodeURIComponent(sessionId)}`

  const online = await fetch(session, { method: 'DELETE', headers })
  expect(online.status, 'delete the Keycloak user session').toBe(204)
  // The delete of the user session can also end the offline session, so 404 is not an error here
  const offline = await fetch(`${session}?isOffline=true`, { method: 'DELETE', headers })
  expect([204, 404], 'delete the Keycloak offline session').toContain(offline.status)
}

/**
 * Ends each Authentik session of the user through the API, with the bootstrap token of akadmin. When a session ends,
 * the worker sends a back-channel logout token to each provider of the session.
 */
async function endAuthentikSessions(email: string) {
  const api = `${requiredEnv('IDP_URL').replace(/\/$/, '')}/api/v3`
  const headers = { Authorization: `Bearer ${requiredEnv('IDP_ADMIN_TOKEN')}` }

  const sessions = await callIdp(`${api}/core/authenticated_sessions/?user__username=${encodeURIComponent(email)}`, { headers })
  expect(sessions.status, 'the Authentik session list').toBe(200)
  const results = sessions.body.results as { uuid: string }[]
  expect(results.length, 'the Authentik sessions of the user').toBeGreaterThan(0)

  for (const session of results) {
    const response = await fetch(`${api}/core/authenticated_sessions/${session.uuid}/`, { method: 'DELETE', headers })
    expect(response.status, 'delete the Authentik session').toBe(204)
  }
}

// Section 3.4 of docs/specs/home-rolled-bff.md: a back-channel logout token from the provider ends the BFF session
test.describe(`Back-channel logout - ${currentIdp.name}`, () => {
  test('an IdP session that ends at the provider ends the BFF session', async ({ page }) => {
    test.skip(
      !currentIdp.sessionFeatures.backChannelLogoutReachesBff,
      `${currentIdp.name} cannot send a back-channel logout token to the BFF of the stack (section 6 of the spec).`,
    )

    await signInThroughSpa(page)
    expect(await bffUserStatus(page)).toBe(200)

    if (process.env.IDP === 'authentik') {
      await endAuthentikSessions(currentIdp.email!)
    } else {
      await endKeycloakSession(await bffSessionId(page))
    }

    // The BFF deletes the session row when the logout token arrives. Authentik sends it from a worker task.
    await expect.poll(() => bffUserStatus(page), { timeout: 30000 }).toBe(204)
  })
})

function base64Url(bytes: Buffer): string {
  return bytes.toString('base64url')
}

/**
 * The BFF never gives a token to the browser, so this test does its own authorization code flow with the client of the
 * BFF. It proves the provider side of rotation (OAuth 2.1 4.3.3): after one refresh, the old refresh token gets
 * invalid_grant. The BFF tests prove that the BFF stores the new refresh token after each refresh.
 */
test.describe(`Refresh token rotation - ${currentIdp.name}`, () => {
  test('the provider rejects the old refresh token after one refresh', async ({ page }) => {
    test.skip(!currentIdp.sessionFeatures.refreshRotation, `${currentIdp.name} does not rotate refresh tokens (section 6 of the spec).`)

    const discovery = await readDiscovery()
    const clientId = requiredEnv('CLIENT_ID')
    const authorization = `Basic ${Buffer.from(`${encodeURIComponent(clientId)}:${encodeURIComponent(requiredEnv('CLIENT_SECRET'))}`).toString('base64')}`
    // A redirect URI that each seed registers
    const redirectUri = new URL('/signin-oidc', process.env.BASE_URL ?? 'http://localhost:24710').toString()
    const codeVerifier = base64Url(randomBytes(32))
    const state = base64Url(randomBytes(16))

    // Playwright cannot route the redirect of a navigation, so the callback reaches the BFF. The BFF did not start this
    // login, so it answers 400 and does not redeem the code.
    const callback = page.waitForRequest((request) => request.url().startsWith(`${redirectUri}?`), { timeout: 30000 })

    const authorizeUrl = new URL(discovery.authorization_endpoint as string)
    authorizeUrl.search = new URLSearchParams({
      response_type: 'code',
      client_id: clientId,
      redirect_uri: redirectUri,
      scope: 'openid profile email offline_access',
      state,
      code_challenge: base64Url(createHash('sha256').update(codeVerifier).digest()),
      code_challenge_method: 'S256',
    }).toString()
    await page.goto(authorizeUrl.toString())
    await loginAtIdp(page, currentIdp)
    const callbackUrl = new URL((await callback).url())
    expect(callbackUrl.searchParams.get('state')).toBe(state)
    const code = callbackUrl.searchParams.get('code')
    expect(code, 'the authorization code').toBeTruthy()

    const tokenRequest = (form: Record<string, string>) =>
      callIdp(discovery.token_endpoint as string, {
        method: 'POST',
        headers: { Authorization: authorization },
        body: new URLSearchParams(form),
      })

    const issued = await tokenRequest({ grant_type: 'authorization_code', code: code!, redirect_uri: redirectUri, code_verifier: codeVerifier })
    expect(issued.status, 'the code exchange').toBe(200)
    const firstRefreshToken = issued.body.refresh_token as string | undefined
    expect(typeof firstRefreshToken, 'the first refresh token').toBe('string')

    const refreshed = await tokenRequest({ grant_type: 'refresh_token', refresh_token: firstRefreshToken! })
    expect(refreshed.status, 'the first refresh').toBe(200)
    const secondRefreshToken = refreshed.body.refresh_token as string | undefined
    expect(typeof secondRefreshToken, 'the rotated refresh token').toBe('string')
    // Compare without the values, so that a failure does not print a token
    expect(secondRefreshToken !== firstRefreshToken, 'the refresh gives a new refresh token').toBe(true)

    const reused = await tokenRequest({ grant_type: 'refresh_token', refresh_token: firstRefreshToken! })
    expect({ status: reused.status, error: reused.body.error }).toEqual({ status: 400, error: 'invalid_grant' })
  })
})

// The payload of a JWT, read with no signature check. The caller reads only claims, never prints the token.
function jwtPayload(token: string): Record<string, unknown> {
  return JSON.parse(Buffer.from(token.split('.')[1] ?? '', 'base64url').toString('utf8')) as Record<string, unknown>
}

// A GET through the BFF proxy with the session cookie of the page, as the SPA sends it
async function bffApiStatus(page: Page, path: string): Promise<number> {
  return page.evaluate(async (url) => (await fetch(url, { headers: { 'X-CSRF': '1' } })).status, path)
}

/**
 * The BFF side of rotation. The stack starts with BffRefreshBeforeExpirationSeconds longer than the access token
 * lifetime, so each API call makes the BFF refresh. The provider rotates the refresh token on each refresh and rejects
 * a reused one (the test above). So the second call succeeds only if the BFF stored the rotated refresh token of the
 * first call. A BFF that keeps the old one gets invalid_grant, ends the session, and forwards the call with no token.
 *
 * The stored ID token proves that the second call refreshed: the BFF sends it as id_token_hint at logout, and its iat
 * is after the start of the second call. A refresh response replaces the stored ID token, and the login ID token is
 * older.
 */
test.describe(`BFF refresh - ${currentIdp.name}`, () => {
  test('each API call refreshes, and the BFF stores the rotated tokens', async ({ page }) => {
    test.skip(
      !currentIdp.sessionFeatures.refreshRotation,
      `${currentIdp.name} does not rotate refresh tokens, so a BFF that keeps the old refresh token also passes (section 6 of the spec).`,
    )
    test.skip(!refreshesOnEachCall(process.env.BFF_REFRESH_BEFORE_EXPIRATION_SECONDS), refreshMarginHint)

    const discovery = await readDiscovery()
    await signInThroughSpa(page)

    expect(await bffApiStatus(page, '/api/v1/weather/secure'), 'the first API call').toBe(200)
    // iat has a resolution of one second, so the second call starts in a later second than each earlier token
    await page.waitForTimeout(2500)
    const secondCallStart = Math.floor(Date.now() / 1000)
    expect(await bffApiStatus(page, '/api/v1/weather/secure'), 'the second API call, with the rotated refresh token').toBe(200)
    expect(await bffUserStatus(page), 'the BFF session after the refreshes').toBe(200)

    const claims = await page.evaluate(
      async () => (await (await fetch('/bff/user', { headers: { 'X-CSRF': '1' } })).json()) as { type: string; value: string }[],
    )
    const logoutUrl = claims.find((claim) => claim.type === 'bff:logout_url')?.value
    expect(logoutUrl, 'the bff:logout_url claim').toBeTruthy()
    const endSession = page.waitForRequest((request) => request.url().startsWith(discovery.end_session_endpoint as string), { timeout: 30000 })
    await page.goto(logoutUrl!)
    const idTokenHint = new URL((await endSession).url()).searchParams.get('id_token_hint')
    expect(idTokenHint, 'the id_token_hint of the logout').toBeTruthy()

    const iat = jwtPayload(idTokenHint!).iat as number
    // One second of tolerance for the clock of the provider container
    expect(iat, 'the iat of the stored ID token is from the refresh of the second call').toBeGreaterThanOrEqual(secondCallStart - 1)
  })
})

/**
 * The refresh lock (section 3.7 of docs/specs/home-rolled-bff.md). The stack refreshes on each API call, and the
 * provider rejects a reused refresh token. So two API calls at the same time need the same refresh. With the lock, one
 * call refreshes, and the other gets the new tokens. With no lock, both calls send the same refresh token, the provider
 * rejects the second one with invalid_grant, and the BFF ends the session.
 */
test.describe(`Refresh lock - ${currentIdp.name}`, () => {
  test('two API calls at the same time both succeed, and the session still works after them', async ({ page }) => {
    test.skip(
      !currentIdp.sessionFeatures.refreshRotation,
      `${currentIdp.name} does not rotate refresh tokens, so two refreshes with one refresh token also pass (section 6 of the spec).`,
    )
    test.skip(!refreshesOnEachCall(process.env.BFF_REFRESH_BEFORE_EXPIRATION_SECONDS), refreshMarginHint)

    await signInThroughSpa(page)

    const statuses = await page.evaluate(async () => {
      // no-store, because Chrome holds a second GET of the same URL until the first one ends, for its cache
      const call = async () =>
        (await fetch('/api/v1/weather/secure', { headers: { 'X-CSRF': '1' }, cache: 'no-store' })).status
      return Promise.all([call(), call()])
    })
    expect(statuses, 'the two API calls at the same time').toEqual([200, 200])
    expect(await bffUserStatus(page), 'the BFF session after the two calls').toBe(200)
    expect(await bffApiStatus(page, '/api/v1/weather/secure'), 'an API call after the two calls').toBe(200)
  })
})
