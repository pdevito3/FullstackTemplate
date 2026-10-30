import type { ReactNode, RefObject } from 'react'

import { cn } from '@/lib/utils'

interface PageHeadingProps {
  children: ReactNode
  className?: string
  ref?: RefObject<HTMLHeadingElement | null>
}

// The root layout moves the focus to the heading after a navigation. tabIndex lets a page move it there after an action.
export function PageHeading({ children, className, ref }: PageHeadingProps) {
  return (
    <h1 ref={ref} tabIndex={-1} className={cn('text-3xl font-bold tracking-tight wrap-anywhere outline-none', className)}>
      {children}
    </h1>
  )
}
