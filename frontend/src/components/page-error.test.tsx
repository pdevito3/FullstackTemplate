import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { PageError } from './page-error'

test('says that the page did not load when its code did not load, and offers a reload', async () => {
  // Arrange and Act
  const screen = await render(
    <PageError error={new TypeError('Failed to fetch dynamically imported module: http://localhost/assets/about-1.js')} reset={() => {}} />,
  )

  // Assert
  const alert = screen.getByRole('alert')
  await expect.element(alert.getByRole('heading', { level: 1, name: 'This page did not load' })).toBeVisible()
  await expect.element(alert.getByText('Check your connection, then reload the page.')).toBeVisible()
  await expect.element(alert.getByRole('button', { name: 'Reload the page' })).toBeVisible()
})

test('says that the page stopped working when the page throws, and shows no text of the error', async () => {
  // Arrange and Act
  const screen = await render(<PageError error={new Error('Cannot read properties of undefined')} reset={() => {}} />)

  // Assert
  const alert = screen.getByRole('alert')
  await expect.element(alert.getByRole('heading', { level: 1, name: 'This page stopped working' })).toBeVisible()
  await expect.element(alert.getByText('Reload the page to try again.')).toBeVisible()
  await expect.element(alert.getByRole('button', { name: 'Reload the page' })).toBeVisible()
  await expect.element(alert.getByText('Cannot read properties of undefined')).not.toBeInTheDocument()
})
