import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import { PageHeading } from '@/components/page-heading'

export function NotFound() {
  return (
    <div className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
      <p className="text-sm font-medium text-muted-foreground">404</p>
      <PageHeading className="mt-2">Page not found</PageHeading>
      <p className="mt-2 text-muted-foreground">No page exists at this address.</p>
      <Link to="/" className={buttonVariants({ className: 'mt-6' })}>
        Go to the home page
      </Link>
    </div>
  )
}
