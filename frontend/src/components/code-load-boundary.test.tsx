import { Component, lazy, Suspense, type ReactNode } from 'react'
import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'

import { CodeLoadBoundary } from './code-load-boundary'

const NotLoaded = lazy(() =>
  Promise.reject(new TypeError('Failed to fetch dynamically imported module: http://localhost/assets/date-filter-1.js')),
)

function Broken(): never {
  throw new TypeError('Cannot read properties of undefined')
}

/** Stands in for the boundary of the route, which shows the page error. */
class RouteBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }

  static getDerivedStateFromError() {
    return { failed: true }
  }

  render() {
    return this.state.failed ? <p>The page error</p> : this.props.children
  }
}

test('shows the fallback in place of a lazy component whose code did not load', async () => {
  // Arrange and Act
  const screen = await render(
    <RouteBoundary>
      <CodeLoadBoundary fallback={<p>The code did not load</p>}>
        <Suspense>
          <NotLoaded />
        </Suspense>
      </CodeLoadBoundary>
    </RouteBoundary>,
  )

  // Assert
  await expect.element(screen.getByText('The code did not load')).toBeVisible()
  await expect.element(screen.getByText('The page error')).not.toBeInTheDocument()
})

test('lets another error go on to the next boundary', async () => {
  // Arrange and Act
  const screen = await render(
    <RouteBoundary>
      <CodeLoadBoundary fallback={<p>The code did not load</p>}>
        <Broken />
      </CodeLoadBoundary>
    </RouteBoundary>,
  )

  // Assert
  await expect.element(screen.getByText('The page error')).toBeVisible()
  await expect.element(screen.getByText('The code did not load')).not.toBeInTheDocument()
})
