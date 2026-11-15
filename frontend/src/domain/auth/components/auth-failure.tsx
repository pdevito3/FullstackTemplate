import type { ReactNode } from 'react'

import { PageHeading } from '@/components/page-heading'

interface AuthFailureProps {
  title: string
  action: string
  children: ReactNode
}

export function AuthFailure({ title, action, children }: AuthFailureProps) {
  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <div className="flex max-w-md flex-col items-center text-center">
        <PageHeading>{title}</PageHeading>
        <p className="mt-2 text-muted-foreground">{action}</p>
        {children}
      </div>
    </main>
  )
}
