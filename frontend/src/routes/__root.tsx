import {
  createRootRoute,
  Outlet,
  useRouter,
  useRouterState,
  type NavigateOptions,
  type ToOptions,
} from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'
import { RouterProvider } from 'react-aria-components'
import {
  SidebarInset,
  SidebarProvider,
  SidebarTrigger,
} from '@/components/ui/sidebar'
import { AppSidebar } from '@/components/app-sidebar'
import { Separator } from '@/components/ui/separator'
import {
  Breadcrumb,
  BreadcrumbItem,
  BreadcrumbList,
  BreadcrumbPage,
} from '@/components/ui/breadcrumb'

// Lets React Aria links (href) navigate with TanStack Router.
declare module 'react-aria-components' {
  interface RouterConfig {
    href: ToOptions['to']
    routerOptions: Omit<NavigateOptions, keyof ToOptions>
  }
}

export const Route = createRootRoute({
  component: RootComponent,
})

const routeNames: Record<string, string> = {
  '/': 'Home',
  '/about': 'About',
  '/components': 'Components',
  '/filter-demo': 'Filter Builder',
}

function RootComponent() {
  const router = useRouter()
  const routerState = useRouterState()
  const currentPath = routerState.location.pathname
  const isNotFound = routerState.matches.some((match) => match.globalNotFound)
  const pageName = isNotFound ? 'Not Found' : (routeNames[currentPath] ?? 'Page')

  return (
    <RouterProvider
      navigate={(to, options) => router.navigate({ ...options, to })}
      useHref={(to) => router.buildLocation({ to }).href}
    >
      <SidebarProvider>
        <AppSidebar />
        <SidebarInset>
          <header className="flex h-16 shrink-0 items-center gap-2 transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-12">
            <div className="flex items-center gap-2 px-4">
              <SidebarTrigger className="-ml-1" />
              <Separator
                orientation="vertical"
                className="mr-2 aria-[orientation=vertical]:h-4 aria-[orientation=vertical]:self-center"
              />
              <Breadcrumb>
                <BreadcrumbList>
                  <BreadcrumbItem>
                    <BreadcrumbPage>{pageName}</BreadcrumbPage>
                  </BreadcrumbItem>
                </BreadcrumbList>
              </Breadcrumb>
            </div>
          </header>
          <div className="flex flex-1 flex-col gap-4 p-4 pt-0">
            <Outlet />
          </div>
        </SidebarInset>
        {/* The default container is a footer, which is a contentinfo landmark. */}
        <TanStackRouterDevtools position="top-right" containerElement="div" />
      </SidebarProvider>
    </RouterProvider>
  )
}
