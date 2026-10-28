import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from '@tanstack/react-router'
import { afterEach, expect, test, vi } from 'vitest'
import { userEvent } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Link } from '@/components/ui/link'

const rootRoute = createRootRoute({ component: Outlet })
const homeRoute = createRoute({ getParentRoute: () => rootRoute, path: '/', component: Home })
const userRoute = createRoute({ getParentRoute: () => rootRoute, path: '/users/$id', component: User })
const routeTree = rootRoute.addChildren([homeRoute, userRoute])
type TestRouter = ReturnType<typeof createTestRouter>

const onPress = vi.fn()

function createTestRouter() {
  return createRouter({ routeTree, history: createMemoryHistory({ initialEntries: ['/'] }) })
}

function Home() {
  return (
    <Link<TestRouter, '/', '/users/$id'> to="/users/$id" params={{ id: 'u_1' }} onPress={onPress}>
      Open user
    </Link>
  )
}

function User() {
  const { id } = userRoute.useParams()
  return <h1>User {id}</h1>
}

async function renderHome() {
  const router = createTestRouter()
  const screen = await render(<RouterProvider router={router} />)
  const link = screen.getByRole('link', { name: 'Open user' })
  await expect.element(link).toBeVisible()
  return { router, screen, link }
}

afterEach(() => {
  onPress.mockClear()
})

test('a link to a route with params has the path with the params as its href', async () => {
  const { link } = await renderHome()

  await expect.element(link).toHaveAttribute('href', '/users/u_1')
})

test('a press on a link goes to the route with the params and calls onPress', async () => {
  const { router, screen, link } = await renderHome()

  await userEvent.click(link)

  await expect.element(screen.getByRole('heading', { name: 'User u_1' })).toBeVisible()
  expect(router.state.location.pathname).toBe('/users/u_1')
  expect(onPress).toHaveBeenCalledOnce()
})

test('a link gets the React Aria focus ring from the keyboard, and Enter goes to the route', async () => {
  const { router, screen, link } = await renderHome()

  await userEvent.tab()
  await expect.element(link).toHaveAttribute('data-focus-visible', 'true')
  await userEvent.keyboard('{Enter}')

  await expect.element(screen.getByRole('heading', { name: 'User u_1' })).toBeVisible()
  expect(router.state.location.pathname).toBe('/users/u_1')
})

test.each([
  { name: 'a cmd-click or ctrl-click', event: 'click', options: { modifiers: ['ControlOrMeta'] } },
  { name: 'a shift-click', event: 'click', options: { modifiers: ['Shift'] } },
  { name: 'a middle-click', event: 'auxclick', options: { button: 'middle' } },
] as const)('$name on a link is left to the browser, so that it opens a new tab or window', async ({ event, options }) => {
  const { router, link } = await renderHome()
  // React Aria stops the event in the React handlers, so keep it from the capture phase and read
  // the state of the event after the handlers ran.
  const events: Event[] = []
  const keep = (e: Event) => events.push(e)
  window.addEventListener(event, keep, { capture: true })

  try {
    await userEvent.click(link, options)
  } finally {
    window.removeEventListener(event, keep, { capture: true })
  }

  expect(events.map((e) => e.defaultPrevented)).toEqual([false])
  expect(router.state.location.pathname).toBe('/')
})

test('a link to a route with params does not compile without the params', () => {
  const links = [
    // @ts-expect-error The route /users/$id needs the id param.
    <Link<TestRouter, '/', '/users/$id'> to="/users/$id">User</Link>,
    // @ts-expect-error The param of /users/$id is id, not userId.
    <Link<TestRouter, '/', '/users/$id'> to="/users/$id" params={{ userId: 'u_1' }}>User</Link>,
  ]

  expect(links).toHaveLength(2)
})

test('a disabled link is marked disabled and does not navigate or call onPress', async () => {
  const disabledRoot = createRootRoute({
    component: () => (
      <Link<TestRouter, '/', '/users/$id'> to="/users/$id" params={{ id: 'u_1' }} disabled onPress={onPress}>
        Open user
      </Link>
    ),
  })
  const router = createRouter({ routeTree: disabledRoot, history: createMemoryHistory({ initialEntries: ['/'] }) })
  const screen = await render(<RouterProvider router={router} />)
  const link = screen.getByRole('link', { name: 'Open user' })
  await expect.element(link).toHaveAttribute('aria-disabled', 'true')

  // Playwright does not click an element with aria-disabled. A screen reader clicks it in this way.
  const element = link.element() as HTMLElement
  element.click()

  expect(router.state.location.pathname).toBe('/')
  expect(onPress).not.toHaveBeenCalled()
})
