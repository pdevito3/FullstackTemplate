import { createRootRoute, Link, Outlet } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/react-router-devtools'

export const Route = createRootRoute({
  component: RootComponent,
})

function RootComponent() {
  return (
    <>
      <nav className="fixed top-0 left-0 right-0 z-50 bg-black/30 backdrop-blur-md border-b border-white/10">
        <div className="max-w-[1400px] mx-auto px-8 py-3 flex gap-6 max-md:px-4">
          <Link
            to="/"
            className="text-text-secondary font-medium text-sm transition-colors hover:text-text-primary [&.active]:text-white [&.active]:font-semibold"
          >
            Home
          </Link>
          <Link
            to="/about"
            className="text-text-secondary font-medium text-sm transition-colors hover:text-text-primary [&.active]:text-white [&.active]:font-semibold"
          >
            About
          </Link>
        </div>
      </nav>
      <div className="pt-12">
        <Outlet />
      </div>
      <TanStackRouterDevtools />
    </>
  )
}
