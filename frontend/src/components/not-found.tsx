import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'

export function NotFound() {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
      <p className="text-sm font-medium text-muted-foreground">404</p>
      <h1 className="mt-2 text-3xl font-bold tracking-tight">Page not found</h1>
      <p className="mt-2 text-muted-foreground">No page exists at this address.</p>
      <Link to="/" className={buttonVariants({ className: 'mt-6' })}>
        Go to the home page
      </Link>
    </div>
  )
}
