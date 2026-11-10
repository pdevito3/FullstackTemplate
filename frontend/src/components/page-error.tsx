import type { ErrorComponentProps } from '@tanstack/react-router'
import { isModuleNotFoundError } from '@tanstack/router-core'
import { PageHeading } from '@/components/page-heading'
import { Button } from '@/components/ui/button'

const codeDidNotLoad = { title: 'This page did not load', action: 'Check your connection, then reload the page.' }
const pageStopped = { title: 'This page stopped working', action: 'Reload the page to try again.' }

// The router shows this in place of the page, so the layout and the menu stay. For a missing code file of a route, as
// after a deploy, the router reloads the page one time first. A failed import stays failed until a reload, so the
// action is a reload.
export function PageError({ error }: ErrorComponentProps) {
  const message = isModuleNotFoundError(error) ? codeDidNotLoad : pageStopped

  return (
    <div role="alert" className="mx-auto flex max-w-md flex-col items-center px-4 py-16 text-center">
      <PageHeading>{message.title}</PageHeading>
      <p className="mt-2 text-muted-foreground">{message.action}</p>
      <Button className="mt-6" onPress={() => window.location.reload()}>
        Reload the page
      </Button>
    </div>
  )
}
