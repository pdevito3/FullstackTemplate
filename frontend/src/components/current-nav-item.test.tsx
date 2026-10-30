import { createMemoryHistory, createRootRoute, createRoute, createRouter, RouterProvider } from '@tanstack/react-router'
import { describe, expect, it } from 'vitest'
import { render } from 'vitest-browser-react'

import { useCurrentNavItem } from '@/components/current-nav-item'

const navUrls = ['/', '/about', '/projects']

async function currentAt(path: string, urls: readonly string[] = navUrls) {
  function CurrentItem() {
    return <output>{JSON.stringify(useCurrentNavItem(urls) ?? null)}</output>
  }
  const rootRoute = createRootRoute({ component: CurrentItem, notFoundComponent: () => null })
  const routeTree = rootRoute.addChildren(
    ['/', '/about', '/start-tenant', '/projects', '/projects/new', '/projects/$id', '/projects/$id/edit'].map((routePath) =>
      createRoute({ getParentRoute: () => rootRoute, path: routePath }),
    ),
  )
  const router = createRouter({ routeTree, history: createMemoryHistory({ initialEntries: [path] }) })
  const screen = await render(<RouterProvider router={router} />)
  const output = screen.getByRole('status')
  await expect.element(output).toBeInTheDocument()
  const current = JSON.parse(output.element().textContent ?? '') as unknown
  await screen.unmount()
  return current
}

describe('useCurrentNavItem', () => {
  it('gives the item of the current page as the page', async () => {
    expect(await currentAt('/projects')).toEqual({ url: '/projects', current: 'page' })
  })

  it('gives the item of a page under the current page as the current item, not the page', async () => {
    expect(await currentAt('/projects/prj_1')).toEqual({ url: '/projects', current: 'true' })
    expect(await currentAt('/projects/prj_1/edit')).toEqual({ url: '/projects', current: 'true' })
  })

  it('ignores the search of the location', async () => {
    expect(await currentAt('/projects?page=2')).toEqual({ url: '/projects', current: 'page' })
  })

  it('does not match an item to a path that only starts with the same text', async () => {
    expect(await currentAt('/projectsx')).toBeNull()
    expect(await currentAt('/aboutx')).toBeNull()
  })

  it('gives the / item only on the home page', async () => {
    expect(await currentAt('/')).toEqual({ url: '/', current: 'page' })
    expect(await currentAt('/start-tenant')).toBeNull()
    expect(await currentAt('/no-such-page')).toBeNull()
    expect(await currentAt('/about')).toEqual({ url: '/about', current: 'page' })
  })

  it('gives the most specific item when two items match, in each order of the items', async () => {
    const urls = ['/projects', '/projects/new']

    expect(await currentAt('/projects/new', urls)).toEqual({ url: '/projects/new', current: 'page' })
    expect(await currentAt('/projects/new', [...urls].reverse())).toEqual({ url: '/projects/new', current: 'page' })
    expect(await currentAt('/projects/new/step', urls)).toEqual({ url: '/projects/new', current: 'true' })
    expect(await currentAt('/projects/new/step', [...urls].reverse())).toEqual({ url: '/projects/new', current: 'true' })
  })
})
