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

// The routes of this test are not in the route tree that the app registers, so a link to them needs this cast.
const testPath = (path: string) => path as '/'

async function renderLayoutAt(path: string) {
  const rootRoute = createRootRoute({ component: RootRoute.options.component })
  const userRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/users/$id',
    staticData: { breadcrumb: 'User' },
    component: () => (
      <>
        <h1>User</h1>
        <p>The details of the user</p>
      </>
    ),
  })
  const linksRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/links',
    staticData: { breadcrumb: 'Links' },
    component: () => (
      <>
        <h1>Links</h1>
        <Link to={testPath('/users/u_2')}>Open the user</Link>
        <Link to={testPath('/plain')}>Open the plain page</Link>
      </>
    ),
  })
  const plainRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/plain',
    staticData: { breadcrumb: 'Plain' },
    component: () => <p>A page with no heading</p>,
  })
  const router = createRouter({
    routeTree: rootRoute.addChildren([userRoute, linksRoute, plainRoute]),
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
  return screen
}

test('the breadcrumb of a route with params shows the name that the route declares', async () => {
  const screen = await renderLayoutAt('/users/u_1')

  await expect.element(screen.getByRole('navigation', { name: 'breadcrumb' }).getByRole('link')).toHaveTextContent('User')
})

test('a link in the page moves the focus to the level 1 heading of the new page', async () => {
  const screen = await renderLayoutAt('/links')
  await userEvent.click(screen.getByRole('link', { name: 'Open the user' }))

  await expect.element(screen.getByRole('heading', { name: 'User', level: 1 })).toHaveFocus()
})

test('a link in the page moves the focus to the main landmark when the new page has no level 1 heading', async () => {
  const screen = await renderLayoutAt('/links')
  await userEvent.click(screen.getByRole('link', { name: 'Open the plain page' }))

  await expect.element(screen.getByText('A page with no heading')).toBeVisible()
  await expect.element(screen.getByRole('main')).toHaveFocus()
})

test('a click on the content after the focus move does not give the focus to the heading or the content', async () => {
  const screen = await renderLayoutAt('/links')
  await userEvent.click(screen.getByRole('link', { name: 'Open the user' }))
  await expect.element(screen.getByRole('heading', { name: 'User', level: 1 })).toHaveFocus()

  await userEvent.click(screen.getByText('The details of the user'))

  expect(document.activeElement).toBe(document.body)
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
