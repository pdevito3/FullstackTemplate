import { createRootRoute, Link, Outlet } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'

export const Route = createRootRoute({
  component: RootComponent,
})

function RootComponent() {
  return (
    <>
      <nav className="sticky top-0 z-50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60 border-b">
        <div className="max-w-4xl mx-auto px-4 py-3 flex gap-6">
          <Link
            to="/"
            className="text-sm font-medium text-muted-foreground transition-colors hover:text-foreground [&.active]:text-foreground [&.active]:font-semibold"
          >
            Home
          </Link>
          <Link
            to="/about"
            className="text-sm font-medium text-muted-foreground transition-colors hover:text-foreground [&.active]:text-foreground [&.active]:font-semibold"
          >
            About
          </Link>
        </div>
      </nav>
      <Outlet />
      <TanStackRouterDevtools />
    </>
  )
}
