import { test, expect, Locator, Page, Request } from '@playwright/test'
import { loadIdp, type IdpConfig } from './idps.ts'

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
  const serverUrl = process.env.SERVER_URL || 'https://localhost:7554'

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
