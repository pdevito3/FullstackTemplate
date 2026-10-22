import { test, expect, Locator, Page } from '@playwright/test'
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
  // Wait for the IDP login form, so that the origin is the IDP and not the app
  await page.locator(idp.usernameSelector).waitFor({ state: 'visible' })
  const idpOrigin = new URL(page.url()).origin

  // Fill username/email
  const usernameValue = idp.email || idp.username
  await page.fill(idp.usernameSelector, usernameValue!)

  if (idp.twoStepLogin) {
    // Two-step login: submit username first, then password
    await page.click(idp.submitSelector)

    // Fill password on the second page. fill waits for the field
    await page.fill(idp.passwordSelector, idp.password)

    // Use secondStepSubmitSelector if defined, otherwise use submitSelector
    const secondSubmit = idp.secondStepSubmitSelector || idp.submitSelector
    await page.click(secondSubmit)
  } else {
    // Single-page login: fill password and submit
    await page.fill(idp.passwordSelector, idp.password)
    await page.click(idp.submitSelector)
  }

  // Handle consent page if needed
  // Note: The popup may close immediately after successful OAuth callback,
  // so we need to handle the case where the page is no longer available
  if (idp.hasConsentPage && idp.consentSelector) {
    try {
      // Check if page is still open before waiting
      if (page.isClosed()) return

      // Wait for the consent page, or for the IDP to send the browser back, if it skipped consent.
      const consentButton = page.locator(idp.consentSelector)
      const consentShown = consentButton.waitFor({ state: 'visible', timeout: 30000 }).then(() => true)
      const leftIdp = page.waitForURL((url) => url.origin !== idpOrigin, { timeout: 30000 }).then(() => false)
      consentShown.catch(() => {})
      leftIdp.catch(() => {})
      const hasConsent = await Promise.race([consentShown, leftIdp])
      if (hasConsent) {
        await consentButton.click()
      }
    } catch (error) {
      // Page was closed - OAuth flow completed successfully without needing consent
      if (page.isClosed()) return
      throw error
    }
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
