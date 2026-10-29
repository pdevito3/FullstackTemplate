import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import {
  createMemoryHistory,
  createRootRoute,
  createRoute,
  createRouter,
  RouterProvider,
  type AnyRoute,
} from '@tanstack/react-router'
import { expect, test } from 'vitest'
import { render } from 'vitest-browser-react'
import { userEvent } from 'vitest/browser'

import { Link } from '@/components/ui/link'
import { AuthKeys } from '@/domain/auth/apis/auth.keys'
import { Route as RootRoute } from '@/routes/__root'
import { routeTree } from '@/routeTree.gen'

async function renderLayoutAt(path: string) {
  const rootRoute = createRootRoute({ component: RootRoute.options.component })
  const userRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/users/$id',
    staticData: { breadcrumb: 'User' },
    component: () => (
      <>
        <h1>User</h1>
        <button type="button">Next user</button>
      </>
    ),
  })
  // The paths are paths of the app, so that the links type-check. The pages are those of the test.
  const linksRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    staticData: { breadcrumb: 'Links' },
    component: () => (
      <>
        <h1>Links</h1>
        <Link to="/about">Open the page with a heading</Link>
        <Link to="/start-tenant">Open the page with no heading</Link>
      </>
    ),
  })
  const headingRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/about',
    staticData: { breadcrumb: 'Heading' },
    component: () => (
      <>
        <h1>Heading</h1>
        <p>The text of the page</p>
      </>
    ),
  })
  const noHeadingRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/start-tenant',
    staticData: { breadcrumb: 'No heading' },
    component: () => <p>A page with no heading</p>,
  })
  const router = createRouter({
    routeTree: rootRoute.addChildren([userRoute, linksRoute, headingRoute, noHeadingRoute]),
    history: createMemoryHistory({ initialEntries: [path] }),
  })
  // Seed an anonymous session, so that the session and membership queries of the layout send no request.
  const queryClient = new QueryClient()
  queryClient.setQueryData(AuthKeys.user(), null)
  const screen = await render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  await expect.element(screen.getByRole('main')).toBeVisible()
  return { screen, router }
}

test('the breadcrumb of a route with params shows the name that the route declares', async () => {
  const { screen } = await renderLayoutAt('/users/u_1')

  await expect.element(screen.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link')).toHaveTextContent('User')
})

test('a link in the page moves the focus to the level 1 heading of the new page', async () => {
  const { screen } = await renderLayoutAt('/')
  await userEvent.click(screen.getByRole('link', { name: 'Open the page with a heading' }))

  await expect.element(screen.getByRole('heading', { name: 'Heading', level: 1 })).toHaveFocus()
})

test('a link in the page moves the focus to the main landmark when the new page has no level 1 heading', async () => {
  const { screen } = await renderLayoutAt('/')
  await userEvent.click(screen.getByRole('link', { name: 'Open the page with no heading' }))

  await expect.element(screen.getByText('A page with no heading')).toBeVisible()
  await expect.element(screen.getByRole('main')).toHaveFocus()
})

test('a click on the content after the focus move does not give the focus to the heading or the content', async () => {
  const { screen } = await renderLayoutAt('/')
  await userEvent.click(screen.getByRole('link', { name: 'Open the page with a heading' }))
  await expect.element(screen.getByRole('heading', { name: 'Heading', level: 1 })).toHaveFocus()

  await userEvent.click(screen.getByRole('heading', { name: 'Heading', level: 1 }))
  expect(document.activeElement).toBe(document.body)
  await userEvent.click(screen.getByText('The text of the page'))

  expect(document.activeElement).toBe(document.body)
  expect(screen.getByRole('heading', { name: 'Heading', level: 1 }).element()).not.toHaveAttribute('tabindex')
})

test('a click on the content after the focus move to the main landmark does not keep the focus on it', async () => {
  const { screen } = await renderLayoutAt('/')
  await userEvent.click(screen.getByRole('link', { name: 'Open the page with no heading' }))
  await expect.element(screen.getByRole('main')).toHaveFocus()

  await userEvent.click(screen.getByText('A page with no heading'))

  expect(document.activeElement).toBe(document.body)
})

test('a navigation that leaves the focus on a control in the page announces the page', async () => {
  const { screen, router } = await renderLayoutAt('/users/u_1')
  ;(screen.getByRole('button', { name: 'Next user' }).element() as HTMLElement).focus()

  router.history.push('/users/u_2')

  await expect.element(screen.getByTestId('page-announcement')).toHaveTextContent('User')
  await expect.element(screen.getByRole('button', { name: 'Next user' })).toHaveFocus()
})

test('two navigations in a row to pages with the same name each change the text of the status region', async () => {
  const { screen, router } = await renderLayoutAt('/users/u_1')
  const region = screen.getByTestId('page-announcement').element()
  const texts: string[] = []
  new MutationObserver(() => texts.push(region.textContent ?? '')).observe(region, { childList: true, characterData: true, subtree: true })
  const trigger = screen.getByRole('button', { name: 'Toggle Sidebar' }).first()
  ;(trigger.element() as HTMLElement).focus()

  router.history.push('/users/u_2')
  await expect.poll(() => region.textContent).toBe('User')
  router.history.push('/users/u_3')

  await expect.poll(() => texts.slice(-3)).toEqual(['User', '', 'User'])
})

test('each page of the app declares the name that its breadcrumb shows', () => {
  const router = createRouter({ routeTree })
  // A pathless layout or a route with an index child is never the last match, so a child names the page.
  const hasIndexChild = (route: AnyRoute) =>
    Object.values((route.children ?? {}) as Record<string, AnyRoute>).some((child) => child.path === '/')

  const pagesWithoutBreadcrumb = Object.values(router.routesById)
    .filter((route) => !route.isRoot && route.path !== undefined && !hasIndexChild(route))
    .filter((route) => !route.options.staticData?.breadcrumb)
    .map((route) => route.fullPath)

  expect(Object.keys(router.routesById).length).toBeGreaterThan(1)
  expect(pagesWithoutBreadcrumb).toEqual([])
})
