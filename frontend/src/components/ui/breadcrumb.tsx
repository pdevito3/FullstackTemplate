"use client"

import * as React from "react"
import {
  Breadcrumb as BreadcrumbPrimitive,
  Breadcrumbs as BreadcrumbsPrimitive,
  Button as ButtonPrimitive,
  composeRenderProps,
  Dialog,
  type BreadcrumbProps,
  type BreadcrumbsProps,
} from "react-aria-components"
import { HugeiconsIcon } from "@hugeicons/react"
import { ArrowRight01Icon, MoreHorizontalCircle01Icon } from "@hugeicons/core-free-icons"
import { cn } from "@/lib/utils"
import { createRouterLink } from "@/components/ui/create-router-link"
import { RouterLinkPrimitive, type RouterLinkHostProps } from "@/components/ui/link"
import { Popover, PopoverTrigger } from "@/components/ui/popover"

function Breadcrumb({ className, ...props }: React.ComponentProps<"nav">) {
  return (
    <nav
      aria-label="breadcrumb"
      data-slot="breadcrumb"
      className={cn("min-w-0", className)}
      {...props}
    />
  )
}

function BreadcrumbList<T extends object>({
  className,
  ...props
}: BreadcrumbsProps<T>) {
  return (
    <BreadcrumbsPrimitive
      data-slot="breadcrumb-list"
      className={cn(
        "flex items-center gap-1.5 text-sm text-muted-foreground",
        className
      )}
      {...props}
    />
  )
}

function BreadcrumbItem({
  className,
  children,
  separatorClassName,
  ...props
}: BreadcrumbProps & { separatorClassName?: string }) {
  return (
    <BreadcrumbPrimitive
      data-slot="breadcrumb-item"
      className={composeRenderProps(className, (className) =>
        cn(
          "inline-flex items-center gap-1 not-data-current:shrink-0 not-data-current:whitespace-nowrap data-current:min-w-0",
          className
        )
      )}
      {...props}
    >
      {composeRenderProps(children, (children, { isCurrent }) => (
        <>
          {children}
          {!isCurrent && (
            <span
              data-slot="breadcrumb-separator"
              role="presentation"
              aria-hidden="true"
              className={cn("[&>svg]:size-3.5", separatorClassName)}
            >
              <HugeiconsIcon icon={ArrowRight01Icon} strokeWidth={2} />
            </span>
          )}
        </>
      ))}
    </BreadcrumbPrimitive>
  )
}

const BreadcrumbLink = createRouterLink(function BreadcrumbLinkHost({
  className,
  ...props
}: RouterLinkHostProps) {
  return (
    <RouterLinkPrimitive
      data-slot="breadcrumb-link"
      className={cn("transition-colors hover:text-foreground", className)}
      {...props}
    />
  )
})

/**
 * When the name truncates, the current page becomes a button that opens the
 * full name in a popover, because a Tooltip does not open on touch. It stays a
 * button until the name changes, so a resize does not unmount a focused button.
 */
function BreadcrumbPage({ className, children }: { className?: string; children?: string }) {
  const [truncatedName, setTruncatedName] = React.useState<string>()
  const isTruncated = children !== undefined && truncatedName === children
  const measure = React.useCallback(
    (element: HTMLElement | null) => {
      if (!element) return
      const observer = new ResizeObserver(() => {
        if (element.scrollWidth > element.clientWidth) setTruncatedName(children)
        else setTruncatedName((name) => (name === children ? name : undefined))
      })
      observer.observe(element)
      return () => observer.disconnect()
    },
    [children]
  )
  const pageClassName = cn("truncate font-normal text-foreground", className)

  if (!isTruncated) {
    return (
      <span
        ref={measure}
        data-slot="breadcrumb-page"
        role="link"
        aria-disabled="true"
        aria-current="page"
        className={pageClassName}
      >
        {children}
      </span>
    )
  }
  return (
    <PopoverTrigger>
      <ButtonPrimitive
        ref={measure}
        data-slot="breadcrumb-page"
        aria-current="page"
        className={cn(
          "cursor-default rounded-sm text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50",
          pageClassName
        )}
      >
        {children}
      </ButtonPrimitive>
      <Popover placement="bottom start" className="w-auto max-w-[calc(100vw-2rem)]">
        <Dialog aria-label="Current page" className="wrap-anywhere outline-none">
          {children}
        </Dialog>
      </Popover>
    </PopoverTrigger>
  )
}

function BreadcrumbEllipsis({
  className,
  ...props
}: React.ComponentProps<"span">) {
  return (
    <span
      data-slot="breadcrumb-ellipsis"
      role="presentation"
      aria-hidden="true"
      className={cn(
        "flex size-5 items-center justify-center [&>svg]:size-4",
        className
      )}
      {...props}
    >
      <HugeiconsIcon icon={MoreHorizontalCircle01Icon} strokeWidth={2} />
      <span className="sr-only">More</span>
    </span>
  )
}

export {
  Breadcrumb,
  BreadcrumbList,
  BreadcrumbItem,
  BreadcrumbLink,
  BreadcrumbPage,
  BreadcrumbEllipsis,
}
